using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// N28: backups taken before the <c>TvSenderUserAccess</c> table existed must restore
/// successfully (table registered optional), and a current backup round-trips the rows.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_TvSenderUserAccess
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutTable_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-tvsenderaccess?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(backup, new[] { "TvSenderUserAccess" }, ct);
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "TvSenderUserAccess", ct));

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.Empty(await db.TvSenderUserAccess.AsNoTracking().ToListAsync(ct));
    }

    [Fact]
    public async Task ReadFromAsync_CurrentBackup_RestoresRows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-tvsenderaccess?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = userId });
        await db.SaveChangesAsync(ct);

        using var stream = new MemoryStream();
        await backup.WriteToAsync(stream, ct);
        stream.Position = 0;
        db.TvSenderUserAccess.RemoveRange(db.TvSenderUserAccess);
        await db.SaveChangesAsync(ct);

        await backup.ReadFromAsync(stream, ct);

        Assert.Single(await db.TvSenderUserAccess.AsNoTracking().ToListAsync(ct));
    }
}
