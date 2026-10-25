using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// The <c>AddSetupsSingleRowIndex</c> migration (Issue #279): the Setups table is a singleton,
/// but check-then-act creation (startup data upgrade racing the first request or a test seed)
/// could leave a second row behind — readers with no ORDER BY then answered the
/// <see cref="Setup.TvSenderEnabled"/> check from the wrong row and produced bimodal 404s.
/// The migration deduplicates existing rows (the oldest wins, matching the ordered read paths)
/// and enforces the single row with a unique index over a constant expression. Verified by
/// actually running the migrations on real SQLite via <see cref="IMigrator"/>;
/// <c>EnsureCreated</c> would build the schema from the model and skip the migration's SQL.
/// </summary>
public sealed class SetupsSingleRowMigrationTests
{
    // The migration preceding AddSetupsSingleRowIndex: at this point the schema matches the
    // current model minus the single-row index, so duplicate rows can still be seeded.
    private const string PredecessorMigration = "20261024072426_EnforceProgramRuleCursorNullStrandKeyUnique";

    [Fact]
    public async Task Migration_RemovesDuplicateSetupRows_OldestSurvives()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=file:migration-setups-dedupe?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new EventManager());
        var migrator = db.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(PredecessorMigration, ct);

        db.Setups.Add(new Setup { ApplicationTitle = "alt", TvSenderEnabled = true });
        db.Setups.Add(new Setup { ApplicationTitle = "neu" });
        await db.SaveChangesAsync(ct);

        await migrator.MigrateAsync(cancellationToken: ct);

        db.ChangeTracker.Clear();
        var setup = await db.Setups.SingleAsync(ct);
        Assert.Equal("alt", setup.ApplicationTitle);
        Assert.True(setup.TvSenderEnabled);
    }

    [Fact]
    public async Task Migration_SingleRowIndex_RejectsSecondSetupRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=file:migration-setups-index?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options, new EventManager());
        var migrator = db.Database.GetService<IMigrator>();

        await migrator.MigrateAsync(cancellationToken: ct);

        db.Setups.Add(new Setup());
        await db.SaveChangesAsync(ct);

        db.Setups.Add(new Setup());
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
        Assert.IsType<SqliteException>(ex.InnerException);
    }
}
