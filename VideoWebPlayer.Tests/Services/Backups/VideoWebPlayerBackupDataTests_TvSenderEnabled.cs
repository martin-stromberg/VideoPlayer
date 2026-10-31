using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before <c>Setups.TvSenderEnabled</c> existed (Schritt 26, N25) must restore
/// successfully: the missing column falls back to its registered restore default
/// <see langword="false"/> — the feature stays unreleased until an admin flips it.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_TvSenderEnabled
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutTvSenderEnabledColumn_RestoresDefaultFalse()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-tvsender?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        // Ein Alt-Backup enthaelt die Spalte nicht — der Seed belegt bewusst den Nicht-Default,
        // damit der Restore-Default (false, nicht der seedete Wert) bewiesen wird.
        var setup = await db.Setups.SingleAsync(ct);
        setup.TvSenderEnabled = true;
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Setups", new[] { "TvSenderEnabled" }, ct);
        var columns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "Setups", ct);
        Assert.DoesNotContain("TvSenderEnabled", columns!);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        // The seeded entities are still tracked — clear the change tracker so the query reads
        // the actually-restored row, not the in-memory snapshot from seeding.
        db.ChangeTracker.Clear();
        Assert.False((await db.Setups.SingleAsync(ct)).TvSenderEnabled);
    }
}
