using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before <c>MediaSources.HiddenInMenu</c> existed must restore successfully: the
/// missing column falls back to its registered restore default <see langword="false"/> — every
/// restored source stays visible in the navigation menu.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_HiddenInMenu
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutHiddenInMenuColumn_RestoresDefaultFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-hiddeninmenu?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        // CreateSeededBackupAsync seedet keine MediaSources-Zeile — der Test legt sie selbst an.
        // Der Seed belegt bewusst den Nicht-Default (true), damit der Restore-Default (false,
        // nicht der seedete Wert) bewiesen wird.
        db.MediaSources.Add(new MediaSource
        {
            Name = "Clip-Quelle",
            Path = "/clips",
            Host = "localhost",
            Port = 22,
            HiddenInMenu = true
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "MediaSources", new[] { "HiddenInMenu" }, ct);
        var columns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "MediaSources", ct);
        Assert.DoesNotContain("HiddenInMenu", columns!);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        // The seeded entities are still tracked — clear the change tracker so the query reads
        // the actually-restored row, not the in-memory snapshot from seeding.
        db.ChangeTracker.Clear();
        Assert.False((await db.MediaSources.SingleAsync(ct)).HiddenInMenu);
    }
}
