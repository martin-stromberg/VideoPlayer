using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before <c>Setups.LiveStreamStandbyMediaItemId</c> existed must restore
/// successfully: the missing column stays <see langword="null"/> (nullable, no restore
/// default needed) — the embedded default standby clip applies.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutStandbyColumn_RestoresNull()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-standby?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        var setup = await db.Setups.SingleAsync(ct);
        setup.LiveStreamStandbyMediaItemId = 1234;
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Setups", new[] { "LiveStreamStandbyMediaItemId" }, ct);
        var columns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "Setups", ct);
        Assert.DoesNotContain("LiveStreamStandbyMediaItemId", columns!);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        db.ChangeTracker.Clear();
        Assert.Null((await db.Setups.SingleAsync(ct)).LiveStreamStandbyMediaItemId);
    }
}
