using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ProgramRuleServiceTests_Crud
{
    private static ProgramRuleService CreateService(ApplicationDbContext db) => new(db);

    private static async Task<(PairingTestDb Fixture, ApplicationUser Admin, TVChannel Channel)> SetupAsync(string name, CancellationToken ct)
    {
        var fixture = await PairingTestDb.CreateAsync(name, ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = new ProgramRuleService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        return (fixture, admin, channel);
    }

    [Fact]
    public async Task CreateRule_ValidInput_PersistsRule()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-create", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);

        var input = new ProgramRuleInput("Abendserie", ProgramRuleType.SeriesFixedTime,
            ProgramRuleRecurrence.Weekly, DayOfWeek.Friday, null, new TimeOnly(20, 15), 2, null,
            ProgramRuleMaterialType.TVShow, show.Id, 5, true);
        var rule = await service.CreateRuleAsync(admin.Id, channel.Id, input, ct);

        Assert.True(rule.Id > 0);
        var stored = await fixture.Db.ProgramRules.SingleAsync(ct);
        Assert.Equal("Abendserie", stored.Name);
        Assert.Equal(ProgramRuleType.SeriesFixedTime, stored.RuleType);
        Assert.Equal(ProgramRuleRecurrence.Weekly, stored.Recurrence);
        Assert.Equal(DayOfWeek.Friday, stored.DayOfWeek);
        Assert.Equal(new TimeOnly(20, 15), stored.TimeOfDay);
        Assert.Equal(2, stored.EpisodeCount);
        Assert.Equal(ProgramRuleMaterialType.TVShow, stored.MaterialType);
        Assert.Equal(show.Id, stored.MaterialId);
        Assert.Equal(5, stored.Priority);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task GetRulesAsync_ReturnsRulesOrderedByName()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-list", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);

        await service.CreateRuleAsync(admin.Id, channel.Id, ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with { Name = "B-Regel" }, ct);
        await service.CreateRuleAsync(admin.Id, channel.Id, ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id) with { Name = "A-Regel" }, ct);

        var rules = await service.GetRulesAsync(admin.Id, channel.Id, ct);

        Assert.Equal(new[] { "A-Regel", "B-Regel" }, rules.Select(r => r.Name));
    }

    [Fact]
    public async Task UpdateRule_ChangesFields()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-update", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        var movie = new Movie { Name = "Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        fixture.Db.Movies.Add(movie);
        await fixture.Db.SaveChangesAsync(ct);

        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        var updated = await service.UpdateRuleAsync(admin.Id, rule.Id,
            new ProgramRuleInput("Neu", ProgramRuleType.MoviePrimeTime, ProgramRuleRecurrence.Once,
                null, new DateOnly(2026, 12, 24), new TimeOnly(20, 15), null, TimeSpan.FromMinutes(120),
                ProgramRuleMaterialType.Movie, movie.Id, 9, false), ct);

        Assert.Equal("Neu", updated.Name);
        Assert.Equal(ProgramRuleType.MoviePrimeTime, updated.RuleType);
        Assert.Equal(ProgramRuleRecurrence.Once, updated.Recurrence);
        Assert.Null(updated.DayOfWeek);
        Assert.Equal(new DateOnly(2026, 12, 24), updated.SingleDate);
        Assert.Equal(TimeSpan.FromMinutes(120), updated.SlotDuration);
        Assert.Equal(ProgramRuleMaterialType.Movie, updated.MaterialType);
        Assert.Equal(movie.Id, updated.MaterialId);
        Assert.Equal(9, updated.Priority);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task SetRuleActive_TogglesFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-toggle", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        await service.SetRuleActiveAsync(admin.Id, rule.Id, false, ct);
        Assert.False((await fixture.Db.ProgramRules.SingleAsync(ct)).IsActive);

        await service.SetRuleActiveAsync(admin.Id, rule.Id, true, ct);
        Assert.True((await fixture.Db.ProgramRules.SingleAsync(ct)).IsActive);
    }

    [Fact]
    public async Task DeleteRule_RemovesRule()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-delete", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);
        var rule = await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        await service.DeleteRuleAsync(admin.Id, rule.Id, ct);

        Assert.Equal(0, await fixture.Db.ProgramRules.CountAsync(ct));
    }

    [Fact]
    public async Task CreateRule_DuplicateNameSameChannel_Fails()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await PairingTestDb.CreateAsync("programrule-crud-unique", ct);
        var admin = await fixture.CreateUserAsync("admin@test.com", isAdmin: true);
        var service = CreateService(fixture.Db);
        var channel = await service.GetOrCreateChannelAsync(ct);
        var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
        fixture.Db.TVShows.Add(show);
        await fixture.Db.SaveChangesAsync(ct);

        await service.CreateRuleAsync(admin.Id, channel.Id,
            ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.CreateRuleAsync(admin.Id, channel.Id,
                ProgramRuleServiceTests_Channel.RuleInput(materialId: show.Id), ct));
    }
}
