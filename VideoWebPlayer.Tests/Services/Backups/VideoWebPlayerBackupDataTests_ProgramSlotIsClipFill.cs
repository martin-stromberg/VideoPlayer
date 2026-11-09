using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before <c>ProgramSlots.IsClipFill</c> existed must restore successfully:
/// the missing column falls back to its registered restore default <c>false</c> — restored
/// slots are simply unmarked (they render as single cards, no data loss). A backup of the
/// current version preserves the flag.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_ProgramSlotIsClipFill
{
    private static async Task<ProgramSlot> SeedClipFillSlotAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var channel = new TVChannel { Name = "TV" };
        db.TVChannels.Add(channel);
        await db.SaveChangesAsync(ct);
        var start = DateTime.UtcNow;
        var slot = new ProgramSlot
        {
            TVChannelId = channel.Id,
            Start = start,
            End = start.AddMinutes(1),
            MediaItemId = 1,
            RuleType = ProgramRuleType.MoviePrimeTime,
            IsClipFill = true
        };
        db.ProgramSlots.Add(slot);
        await db.SaveChangesAsync(ct);
        return slot;
    }

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutIsClipFill_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-isclipfill?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        // Ein Alt-Backup enthaelt die Spalte nicht — der Seed belegt bewusst true, damit der
        // Restore-Default (false, nicht der seedete Wert) bewiesen wird.
        await SeedClipFillSlotAsync(db, ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "ProgramSlots", new[] { "IsClipFill" }, ct);
        var columns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "ProgramSlots", ct);
        Assert.DoesNotContain("IsClipFill", columns!);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        // The seeded entities are still tracked — clear the change tracker so the query reads
        // the actually-restored row, not the in-memory snapshot from seeding.
        db.ChangeTracker.Clear();
        var restored = await db.ProgramSlots.SingleAsync(ct);
        Assert.False(restored.IsClipFill);
    }

    [Fact]
    public async Task ReadFromAsync_CurrentBackup_PreservesIsClipFill()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-isclipfill?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        var slot = await SeedClipFillSlotAsync(db, ct);

        using var stream = new MemoryStream();
        await backup.WriteToAsync(stream, ct);
        stream.Position = 0;
        var columns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(stream, "ProgramSlots", ct);
        Assert.Contains("IsClipFill", columns!);

        db.ProgramSlots.RemoveRange(db.ProgramSlots);
        await db.SaveChangesAsync(ct);

        await backup.ReadFromAsync(stream, ct);

        var restored = await db.ProgramSlots.SingleAsync(ct);
        Assert.Equal(slot.Id, restored.Id);
        Assert.True(restored.IsClipFill);
    }
}
