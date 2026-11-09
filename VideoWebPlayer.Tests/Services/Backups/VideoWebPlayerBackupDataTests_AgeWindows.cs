using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before the five age-window <c>Setups</c> columns existed must restore
/// successfully: the missing columns fall back to their registered restore defaults — the
/// delivered window schema (20/22/23 to 06:00) and the restrictive unknown-rating mode.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_AgeWindows
{
    private static readonly string[] AgeWindowColumns =
    [
        nameof(Setup.ProgramScheduleAgeWindow12Local),
        nameof(Setup.ProgramScheduleAgeWindow16Local),
        nameof(Setup.ProgramScheduleAgeWindow18Local),
        nameof(Setup.ProgramScheduleAgeWindowNightEndLocal),
        nameof(Setup.ProgramScheduleUnknownRatingMode)
    ];

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutAgeWindowColumns_RestoresDefaults()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection($"Data Source=file:backuptest-agewindows-{Guid.NewGuid():N}?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        // Seed deliberately non-default values so the test proves the restore defaults.
        var setup = await db.Setups.SingleAsync(ct);
        setup.ProgramScheduleAgeWindow12Local = new TimeOnly(1, 0);
        setup.ProgramScheduleAgeWindow16Local = new TimeOnly(2, 0);
        setup.ProgramScheduleAgeWindow18Local = new TimeOnly(3, 0);
        setup.ProgramScheduleAgeWindowNightEndLocal = new TimeOnly(4, 0);
        setup.ProgramScheduleUnknownRatingMode = ProgramScheduleUnknownRatingMode.Unrestricted;
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Setups", AgeWindowColumns, ct);
        var columns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "Setups", ct);
        foreach (var column in AgeWindowColumns)
            Assert.DoesNotContain(column, columns!);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        // The seeded entity is still tracked — clear the change tracker so the query reads
        // the actually-restored row, not the in-memory snapshot from seeding.
        db.ChangeTracker.Clear();
        var restored = await db.Setups.SingleAsync(ct);
        Assert.Equal(new TimeOnly(20, 0), restored.ProgramScheduleAgeWindow12Local);
        Assert.Equal(new TimeOnly(22, 0), restored.ProgramScheduleAgeWindow16Local);
        Assert.Equal(new TimeOnly(23, 0), restored.ProgramScheduleAgeWindow18Local);
        Assert.Equal(new TimeOnly(6, 0), restored.ProgramScheduleAgeWindowNightEndLocal);
        Assert.Equal(ProgramScheduleUnknownRatingMode.RestrictToNightWindow, restored.ProgramScheduleUnknownRatingMode);
    }
}
