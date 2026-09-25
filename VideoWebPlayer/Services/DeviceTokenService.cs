using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Security;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Issues, validates and revokes per-device API tokens.
    /// </summary>
    public interface IDeviceTokenService
    {
        /// <summary>
        /// Issues a new device token and persists the paired device.
        /// </summary>
        /// <param name="deviceName">Optional device name; falls back to a default when empty.</param>
        /// <param name="createdByUserId">Id of the administrator who created the pairing code.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The plaintext device token. It is only returned once and never stored.</returns>
        Task<string> IssueAsync(string? deviceName, string? createdByUserId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Validates a device token and updates <see cref="PairedDevice.LastUsedAtUtc"/> on success.
        /// </summary>
        /// <param name="token">The plaintext device token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> when the token is known and not revoked; otherwise <c>false</c>.</returns>
        Task<bool> IsValidDeviceTokenAsync(string token, CancellationToken cancellationToken = default);
        /// <summary>
        /// Revokes the device with the specified id.
        /// </summary>
        /// <param name="deviceId">The device id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> when the device was found; otherwise <c>false</c>.</returns>
        Task<bool> RevokeAsync(int deviceId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Renames the device with the specified id.
        /// </summary>
        /// <param name="deviceId">The device id.</param>
        /// <param name="newName">The new display name (1-200 characters after trimming).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> when the device was found and the name is valid; otherwise <c>false</c>.</returns>
        Task<bool> RenameAsync(int deviceId, string newName, CancellationToken cancellationToken = default);
        /// <summary>
        /// Returns all paired devices, most recently issued first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The paired devices.</returns>
        Task<IReadOnlyList<PairedDevice>> GetDevicesAsync(CancellationToken cancellationToken = default);
    }

    internal sealed class DeviceTokenService : IDeviceTokenService
    {
        private const int MaxDeviceNameLength = 200;
        private readonly ApplicationDbContext _db;

        public DeviceTokenService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<string> IssueAsync(string? deviceName, string? createdByUserId, CancellationToken cancellationToken = default)
        {
            var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
            var issuedAtUtc = DateTime.UtcNow;
            var device = new PairedDevice
            {
                // Fallback-Name mit Ausstellungszeitpunkt, damit mehrere namenlose
                // Geraete in der Admin-Liste unterscheidbar bleiben.
                Name = string.IsNullOrWhiteSpace(deviceName)
                    ? $"Geraet vom {issuedAtUtc.ToString("dd.MM.yyyy, HH:mm:ss", CultureInfo.InvariantCulture)}"
                    : deviceName.Trim(),
                TokenHash = HashHelper.Sha256Hex(token),
                IssuedAtUtc = issuedAtUtc,
                LastUsedAtUtc = null,
                RevokedAtUtc = null,
                CreatedByUserId = createdByUserId
            };
            _db.PairedDevices.Add(device);
            await _db.SaveChangesAsync(cancellationToken);
            return token;
        }

        public async Task<bool> IsValidDeviceTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var hash = HashHelper.Sha256Hex(token);
            var device = await _db.PairedDevices
                .FirstOrDefaultAsync(d => d.TokenHash == hash && d.RevokedAtUtc == null, cancellationToken);
            if (device == null)
                return false;

            device.LastUsedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> RevokeAsync(int deviceId, CancellationToken cancellationToken = default)
        {
            var device = await _db.PairedDevices.FindAsync(new object[] { deviceId }, cancellationToken);
            if (device == null)
                return false;

            device.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<IReadOnlyList<PairedDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
        {
            return await _db.PairedDevices
                .AsNoTracking()
                .OrderByDescending(d => d.IssuedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> RenameAsync(int deviceId, string newName, CancellationToken cancellationToken = default)
        {
            var name = newName?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MaxDeviceNameLength)
                return false;

            var device = await _db.PairedDevices.FindAsync(new object[] { deviceId }, cancellationToken);
            if (device == null)
                return false;

            device.Name = name;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
