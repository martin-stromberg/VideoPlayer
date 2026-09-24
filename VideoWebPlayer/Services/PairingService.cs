using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Security;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Describes why a pairing exchange failed.
    /// </summary>
    public enum PairingExchangeErrorKind
    {
        /// <summary>No error.</summary>
        None,
        /// <summary>The request payload is malformed (maps to HTTP 400).</summary>
        InvalidRequest,
        /// <summary>The pairing code is unknown, expired or already consumed (maps to HTTP 401).</summary>
        InvalidCode
    }

    /// <summary>
    /// Internal result object of a pairing exchange attempt.
    /// </summary>
    public sealed class PairingExchangeResult
    {
        private PairingExchangeResult()
        {
        }

        /// <summary>
        /// Gets a value indicating whether the exchange succeeded.
        /// </summary>
        public bool Success { get; private init; }
        /// <summary>
        /// Gets the failure reason.
        /// </summary>
        public PairingExchangeErrorKind Error { get; private init; }
        /// <summary>
        /// Gets the server ECDH public key (Base64-encoded SubjectPublicKeyInfo).
        /// </summary>
        public string? ServerPublicKey { get; private init; }
        /// <summary>
        /// Gets the AES-256-GCM encrypted device token as Base64(nonce | ciphertext | tag).
        /// </summary>
        public string? EncryptedToken { get; private init; }

        /// <summary>
        /// Creates a failed result.
        /// </summary>
        /// <param name="error">The failure reason.</param>
        /// <returns>The failed result.</returns>
        public static PairingExchangeResult Failure(PairingExchangeErrorKind error)
            => new() { Error = error };

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="serverPublicKey">The server ECDH public key (Base64 SPKI).</param>
        /// <param name="encryptedToken">The encrypted device token.</param>
        /// <returns>The successful result.</returns>
        public static PairingExchangeResult Completed(string serverPublicKey, string encryptedToken)
            => new() { Success = true, ServerPublicKey = serverPublicKey, EncryptedToken = encryptedToken };
    }

    /// <summary>
    /// Result of creating a pairing code: the plaintext code plus its server-side expiry.
    /// </summary>
    public sealed class CreatedPairingCode
    {
        /// <summary>
        /// Gets or sets the plaintext pairing code, shown to the user exactly once.
        /// </summary>
        public string Code { get; set; } = "";
        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the expiry timestamp persisted on the server.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>
    /// Metadata of an active pairing code for display purposes. Never exposes the code or its hash.
    /// </summary>
    public sealed class PairingCodeInfo
    {
        /// <summary>
        /// Gets or sets the creation timestamp.
        /// </summary>
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the expiry timestamp.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }
        /// <summary>
        /// Gets or sets the id of the administrator who created the code.
        /// </summary>
        public string? CreatedByUserId { get; set; }
    }

    /// <summary>
    /// Creates and validates one-time pairing codes and exchanges them against device tokens.
    /// </summary>
    public interface IPairingService
    {
        /// <summary>
        /// Creates a new pairing code and returns its plaintext value plus the persisted expiry.
        /// </summary>
        /// <param name="createdByUserId">Id of the administrator creating the code.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The plaintext pairing code (shown to the user exactly once) and its expiry.</returns>
        Task<CreatedPairingCode> CreatePairingCodeAsync(string? createdByUserId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Exchanges a pairing code for an encrypted device token.
        /// </summary>
        /// <param name="request">The exchange request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The exchange result.</returns>
        Task<PairingExchangeResult> ExchangeAsync(PairingExchangeRequest request, CancellationToken cancellationToken = default);
        /// <summary>
        /// Returns metadata of all pairing codes that are neither expired nor consumed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The active pairing codes.</returns>
        Task<IReadOnlyList<PairingCodeInfo>> GetActiveCodesAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class PairingService : IPairingService
    {
        private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        private const int DefaultCodeLength = 8;
        private const int DefaultCodeTtlMinutes = 5;
        private const int MinCodeLength = 4;
        private const int MinCodeTtlMinutes = 1;
        private const int MaxDeviceNameLength = 200;

        private readonly ApplicationDbContext _db;
        private readonly IDeviceTokenService _deviceTokenService;
        private readonly IConfiguration _configuration;

        public PairingService(ApplicationDbContext db, IDeviceTokenService deviceTokenService, IConfiguration configuration)
        {
            _db = db;
            _deviceTokenService = deviceTokenService;
            _configuration = configuration;
        }

        public async Task<CreatedPairingCode> CreatePairingCodeAsync(string? createdByUserId, CancellationToken cancellationToken = default)
        {
            // Konfigurationswerte auf den sinnvollen Bereich klemmen — ein
            // codeLength <= 0 wuerde einen leeren Code bzw. eine
            // ArgumentOutOfRangeException erzeugen, ein ttl <= 0 einen sofort
            // abgelaufenen Code.
            var codeLength = Math.Max(MinCodeLength, _configuration.GetValue("Pairing:CodeLength", DefaultCodeLength));
            var ttlMinutes = Math.Max(MinCodeTtlMinutes, _configuration.GetValue("Pairing:CodeTtlMinutes", DefaultCodeTtlMinutes));
            var code = RandomNumberGenerator.GetString(CodeAlphabet, codeLength);
            var now = DateTime.UtcNow;
            var pairingCode = new PairingCode
            {
                CodeHash = HashHelper.Sha256Hex(code),
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(ttlMinutes),
                ConsumedAtUtc = null,
                CreatedByUserId = createdByUserId
            };
            _db.PairingCodes.Add(pairingCode);
            await _db.SaveChangesAsync(cancellationToken);
            return new CreatedPairingCode
            {
                Code = code,
                CreatedAtUtc = pairingCode.CreatedAtUtc,
                ExpiresAtUtc = pairingCode.ExpiresAtUtc
            };
        }

        public async Task<PairingExchangeResult> ExchangeAsync(PairingExchangeRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null
                || string.IsNullOrWhiteSpace(request.Code)
                || string.IsNullOrWhiteSpace(request.ClientPublicKey))
            {
                return PairingExchangeResult.Failure(PairingExchangeErrorKind.InvalidRequest);
            }

            // Dieselbe Trim-Semantik wie DeviceTokenService.RenameAsync, damit
            // Exchange und Umbenennen denselben Wertebereich akzeptieren.
            var deviceName = request.DeviceName?.Trim();
            if (deviceName != null && deviceName.Length > MaxDeviceNameLength)
            {
                return PairingExchangeResult.Failure(PairingExchangeErrorKind.InvalidRequest);
            }

            using var clientKey = PairingCrypto.TryImportClientPublicKey(request.ClientPublicKey);
            if (clientKey == null)
            {
                return PairingExchangeResult.Failure(PairingExchangeErrorKind.InvalidRequest);
            }

            var now = DateTime.UtcNow;
            var codeHash = HashHelper.Sha256Hex(request.Code);
            // Nur Admin-Codes loesen den Exchange aus — der Kurzcode-Alias eines
            // Bootstrap-Tickets ist hier bewusst nicht einloesbar.
            var pairingCode = await _db.PairingCodes
                .FirstOrDefaultAsync(c => c.CodeHash == codeHash && c.Kind == PairingCodeKind.AdminCode, cancellationToken);
            if (pairingCode == null)
            {
                return PairingExchangeResult.Failure(PairingExchangeErrorKind.InvalidCode);
            }

            // Atomarer Verbrauch: Gueltigkeit und Einmaligkeit werden in einer Operation geprueft,
            // damit ein zwischen Lese- und Update-Zugriff abgelaufener oder verbrauchter Code
            // nicht trotzdem konsumiert wird.
            var consumedRows = await _db.PairingCodes
                .Where(c => c.Id == pairingCode.Id && c.ConsumedAtUtc == null && c.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsumedAtUtc, now), cancellationToken);
            if (consumedRows == 0)
            {
                return PairingExchangeResult.Failure(PairingExchangeErrorKind.InvalidCode);
            }

            using var serverKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var aesKey = serverKey.DeriveKeyFromHmac(clientKey.PublicKey, HashAlgorithmName.SHA256, null, null, null);
            var issued = await _deviceTokenService.IssueAsync(deviceName, pairingCode.CreatedByUserId, cancellationToken);
            var encryptedToken = PairingCrypto.EncryptPayload(aesKey, Encoding.UTF8.GetBytes(issued.Token));

            return PairingExchangeResult.Completed(
                Convert.ToBase64String(serverKey.ExportSubjectPublicKeyInfo()),
                encryptedToken);
        }

        public async Task<IReadOnlyList<PairingCodeInfo>> GetActiveCodesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _db.PairingCodes
                .AsNoTracking()
                .Where(c => c.Kind == PairingCodeKind.AdminCode && c.ConsumedAtUtc == null && c.ExpiresAtUtc > now)
                .OrderByDescending(c => c.CreatedAtUtc)
                .Select(c => new PairingCodeInfo
                {
                    CreatedAtUtc = c.CreatedAtUtc,
                    ExpiresAtUtc = c.ExpiresAtUtc,
                    CreatedByUserId = c.CreatedByUserId
                })
                .ToListAsync(cancellationToken);
        }
    }
}
