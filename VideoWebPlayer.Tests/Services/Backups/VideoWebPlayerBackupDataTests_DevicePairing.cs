using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Ensures that database backups taken before the device pairing feature (tables <c>PairedDevices</c>,
/// <c>PairingCodes</c>, <c>RefreshTokens</c>) and before the QR bootstrap columns
/// (<c>PairingCodes.Kind</c>, <c>PairingCodes.TicketHash</c>) can still be restored, and that a backup of
/// the current version restores all three tables completely.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_DevicePairing
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPairedDevices_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-paireddevices?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        db.PairedDevices.Add(CreatePairedDevice("Wohnzimmer-TV", "device-hash-1", userId));
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "PairedDevices" }, ct);
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "PairedDevices", ct));

        // This must not throw even though the backup lacks the table added by the device pairing feature.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.PairedDevices.AsNoTracking().AnyAsync(ct));
        await AssertPayloadWasRestoredAsync(db, userId, ct);
    }

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPairingCodes_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-pairingcodes?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        db.PairingCodes.Add(CreatePairingCode("code-hash-1", PairingCodeKind.AdminCode, null, userId));
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "PairingCodes" }, ct);
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "PairingCodes", ct));

        // This must not throw even though the backup lacks the table added by the device pairing feature.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.PairingCodes.AsNoTracking().AnyAsync(ct));
        await AssertPayloadWasRestoredAsync(db, userId, ct);
    }

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutRefreshTokens_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-refreshtokens?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        var device = CreatePairedDevice("Schlafzimmer-TV", "device-hash-2", userId);
        db.PairedDevices.Add(device);
        await db.SaveChangesAsync(ct);
        db.RefreshTokens.Add(CreateRefreshToken("refresh-hash-1", userId, device.Id));
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "RefreshTokens" }, ct);
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "RefreshTokens", ct));

        // This must not throw even though the backup lacks the table added by the QR bootstrap feature.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.RefreshTokens.AsNoTracking().AnyAsync(ct));
        await AssertPayloadWasRestoredAsync(db, userId, ct);
    }

    /// <summary>
    /// The realistic case: a backup of an installation from before the device pairing feature lacks all
    /// three tables at once. Afterwards the device list is empty, i.e. previously paired devices are signed
    /// out and have to be paired again.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutAllDevicePairingTables_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-device-tables?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        var device = CreatePairedDevice("Kueche-TV", "device-hash-3", userId);
        db.PairedDevices.Add(device);
        db.PairingCodes.Add(CreatePairingCode("code-hash-3", PairingCodeKind.BootstrapTicket, "ticket-hash-3", userId));
        await db.SaveChangesAsync(ct);
        db.RefreshTokens.Add(CreateRefreshToken("refresh-hash-3", userId, device.Id));
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(
            backup, new[] { "PairedDevices", "PairingCodes", "RefreshTokens" }, ct);
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "PairedDevices", ct));
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "PairingCodes", ct));
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "RefreshTokens", ct));

        // This must not throw even though the backup lacks all three tables.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.PairedDevices.AsNoTracking().AnyAsync(ct));
        Assert.False(await db.PairingCodes.AsNoTracking().AnyAsync(ct));
        Assert.False(await db.RefreshTokens.AsNoTracking().AnyAsync(ct));
        await AssertPayloadWasRestoredAsync(db, userId, ct);
    }

    /// <summary>
    /// A backup of the intermediate version that already had <c>PairingCodes</c> (device pairing by
    /// administrator code) but not yet the QR bootstrap columns <c>PairingCodes.Kind</c> and
    /// <c>PairingCodes.TicketHash</c>: the restore must fill <c>Kind</c> with
    /// <see cref="PairingCodeKind.AdminCode"/> (the only kind that existed back then) and leave
    /// <c>TicketHash</c> at <see langword="null"/>.
    /// </summary>
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutPairingCodeBootstrapColumns_RestoresWithAdminCodeKind()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-pairingcode-columns?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        db.PairingCodes.Add(CreatePairingCode("code-hash-4", PairingCodeKind.BootstrapTicket, "ticket-hash-4", userId));
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(
            backup, "PairingCodes", new[] { "Kind", "TicketHash" }, ct);
        var pairingCodeColumns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "PairingCodes", ct);
        Assert.NotNull(pairingCodeColumns);
        Assert.DoesNotContain("Kind", pairingCodeColumns!);
        Assert.DoesNotContain("TicketHash", pairingCodeColumns!);
        Assert.Contains("CodeHash", pairingCodeColumns!);

        // This must not throw even though the backup lacks the two columns added by the QR bootstrap feature.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restored = await db.PairingCodes.AsNoTracking().SingleAsync(ct);
        Assert.Equal(PairingCodeKind.AdminCode, restored.Kind);
        Assert.Null(restored.TicketHash);
        Assert.Equal("code-hash-4", restored.CodeHash);
    }

    /// <summary>
    /// Counterpart of the legacy cases above: a backup of the current version still contains all three
    /// tables and restores their content completely, so the tables are optional on restore only for older
    /// backups.
    /// </summary>
    [Fact]
    public async Task WriteToAndReadFromAsync_CurrentBackup_RestoresAllDevicePairingTablesCompletely()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-device-tables-roundtrip?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        var device = CreatePairedDevice("Buero-TV", "device-hash-5", userId);
        db.PairedDevices.Add(device);
        db.PairingCodes.Add(CreatePairingCode("code-hash-5", PairingCodeKind.BootstrapTicket, "ticket-hash-5", userId));
        await db.SaveChangesAsync(ct);
        db.RefreshTokens.Add(CreateRefreshToken("refresh-hash-5", userId, device.Id));
        await db.SaveChangesAsync(ct);

        using var stream = new MemoryStream();
        await backup.WriteToAsync(stream, ct);
        stream.Position = 0;
        await backup.ReadFromAsync(stream, ct);

        var restoredDevice = await db.PairedDevices.AsNoTracking().SingleAsync(ct);
        Assert.Equal("Buero-TV", restoredDevice.Name);
        Assert.Equal("device-hash-5", restoredDevice.TokenHash);
        Assert.Equal(userId, restoredDevice.CreatedByUserId);

        var restoredCode = await db.PairingCodes.AsNoTracking().SingleAsync(ct);
        Assert.Equal(PairingCodeKind.BootstrapTicket, restoredCode.Kind);
        Assert.Equal("code-hash-5", restoredCode.CodeHash);
        Assert.Equal("ticket-hash-5", restoredCode.TicketHash);

        var restoredToken = await db.RefreshTokens.AsNoTracking().SingleAsync(ct);
        Assert.Equal("refresh-hash-5", restoredToken.TokenHash);
        Assert.Equal(userId, restoredToken.UserId);
        Assert.Equal(device.Id, restoredToken.DeviceId);
    }

    /// <summary>
    /// Asserts that the restore really wrote the backup's payload back, not just that it ran without an
    /// error: <c>Setups.ContinueWatchingEndThresholdSeconds</c> is 42 only because
    /// <see cref="LegacyBackupArchiveBuilder.CreateSeededBackupAsync"/> seeded it that way and the value
    /// came back out of the archive — <c>Setups</c> is not in <c>OptionalRestoreTables</c>, so nothing else
    /// can produce it. Checking the admin user alone would not prove this, because
    /// <c>VideoWebPlayerBackupData.EnsureAdminAccountAsync</c> restores or recreates that record
    /// independently of the table payloads.
    /// </summary>
    /// <param name="db">The restored database context.</param>
    /// <param name="userId">The id of the seeded administrator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static async Task AssertPayloadWasRestoredAsync(
        ApplicationDbContext db, string userId, CancellationToken cancellationToken)
    {
        var setup = await db.Setups.AsNoTracking().SingleAsync(cancellationToken);
        Assert.Equal(42, setup.ContinueWatchingEndThresholdSeconds);
        Assert.Equal(userId, (await db.Users.AsNoTracking().SingleAsync(cancellationToken)).Id);
    }

    private static PairedDevice CreatePairedDevice(string name, string tokenHash, string userId)
        => new()
        {
            Name = name,
            TokenHash = tokenHash,
            IssuedAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = userId
        };

    private static PairingCode CreatePairingCode(string codeHash, PairingCodeKind kind, string? ticketHash, string userId)
        => new()
        {
            Kind = kind,
            CodeHash = codeHash,
            TicketHash = ticketHash,
            CreatedAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            ExpiresAtUtc = new DateTime(2026, 9, 1, 10, 5, 0, DateTimeKind.Utc),
            CreatedByUserId = userId
        };

    private static RefreshToken CreateRefreshToken(string tokenHash, string userId, int deviceId)
        => new()
        {
            TokenHash = tokenHash,
            UserId = userId,
            DeviceId = deviceId,
            CreatedAtUtc = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc),
            ExpiresAtUtc = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc)
        };
}
