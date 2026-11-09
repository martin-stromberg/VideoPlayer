using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before <c>MovieMediaItems.IsTrailer</c> existed (Schritt 17, trailer material)
/// must restore with the column defaulting to <see langword="false"/>.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_Trailer
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutIsTrailerColumn_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-istrailer?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "MovieMediaItems", new[] { "IsTrailer" }, ct);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
        Assert.All(await db.MovieMediaItems.ToListAsync(ct), l => Assert.False(l.IsTrailer));
    }
}
