using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Services.Security;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Describes why a bootstrap ticket operation failed.
    /// </summary>
    public enum BootstrapTicketErrorKind
    {
        /// <summary>No error.</summary>
        None,
        /// <summary>The request payload is malformed (maps to HTTP 400).</summary>
        InvalidRequest,
        /// <summary>The ticket is unknown, expired or already consumed (maps to HTTP 401).</summary>
        InvalidTicket,
        /// <summary>The user is not allowed to create bootstrap tickets (admin-only mode).</summary>
        Forbidden,
        /// <summary>The per-user rate limit for ticket creation is exceeded.</summary>
        RateLimited
    }

    /// <summary>
    /// Result of creating a bootstrap ticket: the plaintext ticket secret plus the short code alias.
    /// </summary>
    public sealed class CreatedBootstrapTicket
    {
        /// <summary>
        /// Gets or sets the plaintext bootstrap ticket (embedded in the QR code, never stored).
        /// </summary>
        public string Ticket { get; set; } = "";
        /// <summary>
        /// Gets or sets the 8-character short code alias resolving to the same ticket.
        /// </summary>
        public string ShortCode { get; set; } = "";
        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the expiry timestamp persisted on the server.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the failure reason when creation failed.
        /// </summary>
        public BootstrapTicketErrorKind Error { get; set; }
        /// <summary>
        /// Gets a value indicating whether the ticket was created.
        /// </summary>
        public bool Success => Error == BootstrapTicketErrorKind.None;
    }

    /// <summary>
    /// Internal result object of a bootstrap redemption attempt.
    /// </summary>
    public sealed class PairingBootstrapResult
    {
        private PairingBootstrapResult()
        {
        }

        /// <summary>
        /// Gets a value indicating whether the bootstrap succeeded.
        /// </summary>
        public bool Success { get; private init; }
        /// <summary>
        /// Gets the failure reason.
        /// </summary>
        public BootstrapTicketErrorKind Error { get; private init; }
        /// <summary>
        /// Gets the server ECDH public key (Base64-encoded SubjectPublicKeyInfo).
        /// </summary>
        public string? ServerPublicKey { get; private init; }
        /// <summary>
        /// Gets the AES-256-GCM encrypted payload as Base64(nonce | ciphertext | tag).
        /// </summary>
        public string? EncryptedPayload { get; private init; }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="error">The failure reason.</param>
        /// <returns>The failed result.</returns>
        public static PairingBootstrapResult Failure(BootstrapTicketErrorKind error)
            => new() { Error = error };

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="serverPublicKey">The server ECDH public key (Base64 SPKI).</param>
        /// <param name="encryptedPayload">The encrypted payload.</param>
        /// <returns>The successful result.</returns>
        public static PairingBootstrapResult Completed(string serverPublicKey, string encryptedPayload)
            => new() { Success = true, ServerPublicKey = serverPublicKey, EncryptedPayload = encryptedPayload };
    }

    /// <summary>
    /// Creates user-bound bootstrap tickets and redeems them against device token, user session and refresh token.
    /// </summary>
    public interface IPairingBootstrapService
    {
        /// <summary>
        /// Creates a new bootstrap ticket (long secret + 8-character short code alias).
        /// </summary>
        /// <param name="createdByUserId">Id of the user creating the ticket.</param>
        /// <param name="isAdmin">Whether the user is an administrator (for the admin-only option).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The plaintext ticket (embedded in the QR code exactly once) or a failure reason.</returns>
        Task<CreatedBootstrapTicket> CreateBootstrapTicketAsync(string createdByUserId, bool isAdmin, CancellationToken cancellationToken = default);
        /// <summary>
        /// Redeems a bootstrap ticket (or its short code alias) for an encrypted payload containing
        /// device token, user session JWT and refresh token.
        /// </summary>
        /// <param name="request">The bootstrap request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The bootstrap result.</returns>
        Task<PairingBootstrapResult> BootstrapAsync(PairingBootstrapRequest request, CancellationToken cancellationToken = default);
    }

    internal sealed class PairingBootstrapService : IPairingBootstrapService
    {
        private const string ShortCodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int ShortCodeLength = 8;
        private const int TicketSecretBytes = 24;
        private const int DefaultTicketTtlMinutes = 5;
        private const int MinTicketTtlMinutes = 1;
        private const int DefaultMaxTicketsPerHour = 10;
        private const int MinMaxTicketsPerHour = 1;
        private const int MaxDeviceNameLength = 200;

        private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

        private readonly ApplicationDbContext _db;
        private readonly IDeviceTokenService _deviceTokenService;
        private readonly IRefreshTokenService _refreshTokenService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuthorizationTokenService _authorizationTokenService;
        private readonly IConfiguration _configuration;

        public PairingBootstrapService(
            ApplicationDbContext db,
            IDeviceTokenService deviceTokenService,
            IRefreshTokenService refreshTokenService,
            UserManager<ApplicationUser> userManager,
            AuthorizationTokenService authorizationTokenService,
            IConfiguration configuration)
        {
            _db = db;
            _deviceTokenService = deviceTokenService;
            _refreshTokenService = refreshTokenService;
            _userManager = userManager;
            _authorizationTokenService = authorizationTokenService;
            _configuration = configuration;
        }

        public async Task<CreatedBootstrapTicket> CreateBootstrapTicketAsync(string createdByUserId, bool isAdmin, CancellationToken cancellationToken = default)
        {
            if (_configuration.GetValue("Pairing:BootstrapAdminOnly", false) && !isAdmin)
            {
                return new CreatedBootstrapTicket { Error = BootstrapTicketErrorKind.Forbidden };
            }

            var maxPerHour = Math.Max(MinMaxTicketsPerHour, _configuration.GetValue("Pairing:BootstrapMaxTicketsPerHour", DefaultMaxTicketsPerHour));
            var since = DateTime.UtcNow.AddHours(-1);
            var createdLastHour = await _db.PairingCodes
                .CountAsync(c => c.Kind == PairingCodeKind.BootstrapTicket
                                 && c.CreatedByUserId == createdByUserId
                                 && c.CreatedAtUtc > since, cancellationToken);
            if (createdLastHour >= maxPerHour)
            {
                return new CreatedBootstrapTicket { Error = BootstrapTicketErrorKind.RateLimited };
            }

            var ttlMinutes = Math.Max(MinTicketTtlMinutes, _configuration.GetValue("Pairing:BootstrapTicketTtlMinutes", DefaultTicketTtlMinutes));
            var ticket = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TicketSecretBytes));
            var shortCode = RandomNumberGenerator.GetString(ShortCodeAlphabet, ShortCodeLength);
            var now = DateTime.UtcNow;
            var entity = new PairingCode
            {
                Kind = PairingCodeKind.BootstrapTicket,
                CodeHash = HashHelper.Sha256Hex(shortCode),
                TicketHash = HashHelper.Sha256Hex(ticket),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(ttlMinutes),
                ConsumedAtUtc = null,
                CreatedByUserId = createdByUserId
            };
            _db.PairingCodes.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return new CreatedBootstrapTicket
            {
                Ticket = ticket,
                ShortCode = shortCode,
                CreatedAtUtc = entity.CreatedAtUtc,
                ExpiresAtUtc = entity.ExpiresAtUtc
            };
        }

        public async Task<PairingBootstrapResult> BootstrapAsync(PairingBootstrapRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.Ticket)
                || string.IsNullOrWhiteSpace(request.ClientPublicKey))
            {
                return PairingBootstrapResult.Failure(BootstrapTicketErrorKind.InvalidRequest);
            }

            var deviceName = request.DeviceName?.Trim();
            if (deviceName != null && deviceName.Length > MaxDeviceNameLength)
            {
                return PairingBootstrapResult.Failure(BootstrapTicketErrorKind.InvalidRequest);
            }

            using var clientKey = PairingCrypto.TryImportClientPublicKey(request.ClientPublicKey);
            if (clientKey == null)
            {
                return PairingBootstrapResult.Failure(BootstrapTicketErrorKind.InvalidRequest);
            }

            var now = DateTime.UtcNow;
            var ticketHash = HashHelper.Sha256Hex(request.Ticket);
            // Sowohl das lange Ticket (QR) als auch der 8-Zeichen-Kurzcode (Alias)
            // loesen denselben Datensatz auf — Verbrauch und TTL gelten gemeinsam.
            var pairingCode = await _db.PairingCodes
                .FirstOrDefaultAsync(c => c.Kind == PairingCodeKind.BootstrapTicket
                                          && (c.TicketHash == ticketHash || c.CodeHash == ticketHash), cancellationToken);
            if (pairingCode == null)
            {
                return PairingBootstrapResult.Failure(BootstrapTicketErrorKind.InvalidTicket);
            }

            // Atomarer Verbrauch analog zum Exchange — Gueltigkeit und Einmaligkeit
            // werden in einer Operation geprueft.
            var consumedRows = await _db.PairingCodes
                .Where(c => c.Id == pairingCode.Id && c.ConsumedAtUtc == null && c.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsumedAtUtc, now), cancellationToken);
            if (consumedRows == 0)
            {
                return PairingBootstrapResult.Failure(BootstrapTicketErrorKind.InvalidTicket);
            }

            var user = pairingCode.CreatedByUserId != null
                ? await _userManager.FindByIdAsync(pairingCode.CreatedByUserId)
                : null;
            if (user == null)
            {
                return PairingBootstrapResult.Failure(BootstrapTicketErrorKind.InvalidTicket);
            }

            var device = await _deviceTokenService.IssueAsync(deviceName, pairingCode.CreatedByUserId, cancellationToken);
            var authToken = _authorizationTokenService.CreateToken(user);
            var refreshToken = await _refreshTokenService.IssueAsync(user.Id, device.DeviceId, cancellationToken);

            var payload = JsonSerializer.SerializeToUtf8Bytes(new PairingBootstrapPayload
            {
                DeviceToken = device.Token,
                Token = authToken.token,
                Expires = authToken.expires,
                RefreshToken = refreshToken.Token
            }, PayloadJsonOptions);

            using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var aesKey = serverKey.DeriveKeyFromHmac(clientKey.PublicKey, HashAlgorithmName.SHA256, null, null, null);
            var encryptedPayload = PairingCrypto.EncryptPayload(aesKey, payload);

            return PairingBootstrapResult.Completed(
                Convert.ToBase64String(serverKey.ExportSubjectPublicKeyInfo()),
                encryptedPayload);
        }
    }
}
