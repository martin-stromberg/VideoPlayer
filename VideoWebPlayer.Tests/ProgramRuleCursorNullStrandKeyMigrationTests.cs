using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// The data migration inside <c>EnforceProgramRuleCursorNullStrandKeyUnique</c> (Issue #262):
/// existing duplicate NULL-<see cref="ProgramRuleCursor.StrandKey"/> rows are merged per rule —
/// the youngest row wins (highest <see cref="ProgramRuleCursor.UpdatedAt"/>, ties by highest
/// <see cref="ProgramRuleCursor.Id"/>) — before the filtered unique index is created. Verified
/// by actually running the migrations on real SQLite via <see cref="IMigrator"/>;
/// <c>EnsureCreated</c> would build the schema from the model and skip the migration's SQL.
/// </summary>
public sealed class ProgramRuleCursorNullStrandKeyMigrationTests
{
    // The migration preceding EnforceProgramRuleCursorNullStrandKeyUnique: at this point the
    // schema matches the current model minus the new filtered index, so duplicate NULL rows
    // can still be seeded through normal EF entities.
    private const string PredecessorMigration = "20261023055517_AddTvSenderUserAccess";

    [Fact]
    public async Task Migration_MergesDuplicateNullStrandKeyRows_YoungestWins()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=file:migration-nullstrand-merge?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new EventManager());
        var migrator = db.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(PredecessorMigration, ct);

        var rule = await SeedRuleAsync(db, ct);
        db.ProgramRuleCursors.Add(Cursor(rule.Id, lastContentId: 111, updatedAt: new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)));
        db.ProgramRuleCursors.Add(Cursor(rule.Id, lastContentId: 222, updatedAt: new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync(ct);

        await migrator.MigrateAsync(cancellationToken: ct);

        db.ChangeTracker.Clear();
        var cursor = await db.ProgramRuleCursors.SingleAsync(c => c.ProgramRuleId == rule.Id, ct);
        Assert.Equal(222, cursor.LastContentId);
        Assert.Equal(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc), cursor.UpdatedAt);
    }

    [Fact]
    public async Task Migration_NullStrandKeyUniqueIndex_RejectsDuplicateAfterUpgrade()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=file:migration-nullstrand-index?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new EventManager());
        var migrator = db.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(PredecessorMigration, ct);

        var rule = await SeedRuleAsync(db, ct);
        db.ProgramRuleCursors.Add(Cursor(rule.Id, lastContentId: 111, updatedAt: new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync(ct);

        await migrator.MigrateAsync(cancellationToken: ct);

        db.ChangeTracker.Clear();
        db.ProgramRuleCursors.Add(Cursor(rule.Id, lastContentId: 222, updatedAt: new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc)));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
    }

    private static ProgramRuleCursor Cursor(long ruleId, long lastContentId, DateTime updatedAt)
        => new()
        {
            ProgramRuleId = ruleId,
            StrandKey = null,
            LastContentType = ProgramCursorContentType.TVShowEpisode,
            LastContentId = lastContentId,
            UpdatedAt = updatedAt
        };

    private static async Task<ProgramRule> SeedRuleAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var channel = new TVChannel { Name = "Testsender" };
        db.TVChannels.Add(channel);
        var rule = new ProgramRule
        {
            TVChannel = channel,
            Name = "Serienfenster",
            RuleType = ProgramRuleType.GenreRotation,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(12, 0),
            EpisodeCount = 2,
            MaterialType = ProgramRuleMaterialType.Genre,
            MaterialId = 1,
            Priority = 5,
            IsActive = true
        };
        db.ProgramRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }
}
