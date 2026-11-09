using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backup compatibility of the clip-group feature: an old archive without the new tables and
/// without the <c>ProgramRules.PlaceholderGrace</c> column restores cleanly (the grace column
/// is injected with the 15-minute default); new tables and the column round-trip.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_ClipGroups
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutClipGroupTables_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-clipgroups?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(
            await LegacyBackupArchiveBuilder.RemoveTablesAsync(
                backup, new[] { "ProgramClipGroups", "ProgramClipGroupItems", "ProgramRuleClipGroups" }, ct),
            "ProgramRules", new[] { "PlaceholderGrace" }, ct);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.False(await db.ProgramClipGroups.AnyAsync(ct));
        Assert.False(await db.ProgramClipGroupItems.AnyAsync(ct));
        Assert.False(await db.ProgramRuleClipGroups.AnyAsync(ct));
        // Restored rules (if any) carry the injected default grace.
        Assert.All(await db.ProgramRules.ToListAsync(ct), r => Assert.Equal(TimeSpan.FromMinutes(15), r.PlaceholderGrace));
    }

    [Fact]
    public async Task ReadFromAsync_ClipGroupTables_Roundtrip()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-clipgroups-roundtrip?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        using var stream = await LegacyBackupArchiveBuilder.AppendRowsAsync(backup, "ProgramClipGroups",
            new[] { new Dictionary<string, object?>
            {
                ["Id"] = 10L,
                ["Name"] = "Maskottchen",
                ["IsSystem"] = false,
                ["CreatedAt"] = "2026-01-01T00:00:00",
                ["UpdatedAt"] = "2026-01-01T00:00:00"
            } }, ct);

        await backup.ReadFromAsync(stream, ct);

        var group = await db.ProgramClipGroups.FindAsync([10L], ct);
        Assert.NotNull(group);
        Assert.Equal("Maskottchen", group!.Name);
        Assert.False(group.IsSystem);
    }
}
