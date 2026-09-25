using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Security;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Result of issuing a refresh token: the plaintext token plus its expiry.
    /// </summary>
    public sealed class IssuedRefreshToken
    {
        /// <summary>
        /// Gets or sets the plaintext refresh token, returned to the client exactly once.
        /// </summary>
        public string Token { get; set; } = "";
        /// <summary>
        /// Gets or sets the expiry timestamp persisted on the server.
        /// </summary>
        public DateTime ExpiresAtUtc { get; set; }
    }

    /// <summary>
    /// Internal result of a refresh-token rotation attempt.
    /// </summary>
    public sealed class RefreshRotationResult
    {
        private RefreshRotationResult()
        {
        }

        /// <summary>
        /// Gets a value indicating whether the rotation succeeded.
        /// </summary>
        public bool Success { get; private init; }
        /// <summary>
        /// Gets the id of the user the new token belongs to.
        /// </summary>
        public string? UserId { get; private init; }
        /// <summary>
        /// Gets the id of the device the new token is bound to.
        /// </summary>
        public int? DeviceId { get; private init; }
        /// <summary>
        /// Gets the new plaintext refresh token.
        /// </summary>
        public string? NewToken { get; private init; }
        /// <summary>
        /// Gets the expiry timestamp of the new token.
        /// </summary>
        public DateTime? NewExpiresAtUtc { get; private init; }

        /// <summary>
        /// Creates a failed result (the presented token is unknown, expired, revoked or bound to a revoked device).
        /// </summary>
        /// <returns>The failed result.</returns>
        public static RefreshRotationResult Failure() => new();

        /// <summary>
        /// Creates a successful rotation result.
        /// </summary>
        /// <param name="userId">The id of the user the refresh token belongs to.</param>
        /// <param name="deviceId">The id of the device the refresh token is bound to.</param>
        /// <param name="newToken">The newly issued refresh token.</param>
        /// <param name="newExpiresAtUtc">The expiry timestamp of the new token.</param>
        /// <returns>The successful result.</returns>
        public static RefreshRotationResult Rotated(string userId, int deviceId, string newToken, DateTime newExpiresAtUtc)
            => new()
            {
                Success = true,
                UserId = userId,
                DeviceId = deviceId,
                NewToken = newToken,
                NewExpiresAtUtc = newExpiresAtUtc
            };
    }

    /// <summary>
    /// Issues, rotates and revokes classic refresh tokens bound to a user and a paired device.
    /// </summary>
    public interface IRefreshTokenService
    {
        /// <summary>
        /// Issues a new refresh token and persists only its hash.
        /// </summary>
        /// <param name="userId">Id of the token owner.</param>
        /// <param name="deviceId">Id of the paired device the token is bound to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The plaintext token (returned once) and its expiry.</returns>
        Task<IssuedRefreshToken> IssueAsync(string userId, int deviceId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Atomically rotates a refresh token: the presented token is revoked and replaced by a new one.
        /// A token that was already rotated counts as reuse and revokes the whole token family of user and device.
        /// </summary>
        /// <param name="refreshToken">The plaintext refresh token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The rotation result including the new token.</returns>
        Task<RefreshRotationResult> RotateAsync(string refreshToken, CancellationToken cancellationToken = default);
        /// <summary>
        /// Revokes a single refresh token (logout). Idempotent: unknown or already revoked tokens are ignored.
        /// </summary>
        /// <param name="refreshToken">The plaintext refresh token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
        /// <summary>
        /// Revokes all active refresh tokens bound to a device (device revocation).
        /// </summary>
        /// <param name="deviceId">The device id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RevokeAllForDeviceAsync(int deviceId, CancellationToken cancellationToken = default);
    }

    internal sealed class RefreshTokenService : IRefreshTokenService
    {
        private const int DefaultRefreshTokenTtlDays = 30;
        private const int MinRefreshTokenTtlDays = 1;

        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;

        public RefreshTokenService(ApplicationDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        public async Task<IssuedRefreshToken> IssueAsync(string userId, int deviceId, CancellationToken cancellationToken = default)
        {
            var ttlDays = Math.Max(MinRefreshTokenTtlDays, _configuration.GetValue("Auth:RefreshTokenTtlDays", DefaultRefreshTokenTtlDays));
            var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            var now = DateTime.UtcNow;
            var entity = new RefreshToken
            {
                TokenHash = HashHelper.Sha256Hex(token),
                UserId = userId,
                DeviceId = deviceId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(ttlDays),
                RevokedAtUtc = null,
                ReplacedByHash = null
            };
            _db.RefreshTokens.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return new IssuedRefreshToken { Token = token, ExpiresAtUtc = entity.ExpiresAtUtc };
        }

        public async Task<RefreshRotationResult> RotateAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return RefreshRotationResult.Failure();

            var now = DateTime.UtcNow;
            var hash = HashHelper.Sha256Hex(refreshToken);
            // AsNoTracking: Der Datensatz muss den aktuellen Datenbankstand sehen —
            // ExecuteUpdate-Aenderungen (Rotation/Revoke) umgehen den Change-Tracker.
            var existing = await _db.RefreshTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
            if (existing == null)
                return RefreshRotationResult.Failure();

            // Reuse-Detection: Ein bereits rotierter Token darf nicht erneut
            // vorgelegt werden — das deutet auf einen entwendeten Token hin.
            // Die komplette Token-Familie (Benutzer + Geraet) wird gesperrt.
            if (existing.RevokedAtUtc != null)
            {
                await _db.RefreshTokens
                    .Where(t => t.UserId == existing.UserId && t.DeviceId == existing.DeviceId && t.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
                return RefreshRotationResult.Failure();
            }

            if (existing.ExpiresAtUtc <= now)
                return RefreshRotationResult.Failure();

            var device = await _db.PairedDevices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == existing.DeviceId, cancellationToken);
            if (device == null || device.RevokedAtUtc != null)
                return RefreshRotationResult.Failure();

            var newToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            var newHash = HashHelper.Sha256Hex(newToken);

            // Atomare Rotation: Gueltigkeit und Einmaligkeit in einer Operation,
            // damit parallele Refresh-Aufrufe nicht beide durchkommen.
            var rotatedRows = await _db.RefreshTokens
                .Where(t => t.Id == existing.Id && t.RevokedAtUtc == null && t.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAtUtc, now)
                    .SetProperty(t => t.ReplacedByHash, newHash), cancellationToken);
            if (rotatedRows == 0)
                return RefreshRotationResult.Failure();

            var newEntity = new RefreshToken
            {
                TokenHash = newHash,
                UserId = existing.UserId,
                DeviceId = existing.DeviceId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.AddDays(Math.Max(MinRefreshTokenTtlDays, _configuration.GetValue("Auth:RefreshTokenTtlDays", DefaultRefreshTokenTtlDays))),
                RevokedAtUtc = null,
                ReplacedByHash = null
            };
            _db.RefreshTokens.Add(newEntity);
            await _db.SaveChangesAsync(cancellationToken);

            return RefreshRotationResult.Rotated(existing.UserId, existing.DeviceId, newToken, newEntity.ExpiresAtUtc);
        }

        public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return;

            var hash = HashHelper.Sha256Hex(refreshToken);
            var now = DateTime.UtcNow;
            await _db.RefreshTokens
                .Where(t => t.TokenHash == hash && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
        }

        public async Task RevokeAllForDeviceAsync(int deviceId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            await _db.RefreshTokens
                .Where(t => t.DeviceId == deviceId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
        }
    }
}
