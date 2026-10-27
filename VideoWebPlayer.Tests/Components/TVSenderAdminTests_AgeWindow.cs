using Bunit;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the youth-protection fields of the "Einstellungen" card of
/// <see cref="TVSenderAdmin"/>: the four window times, the unknown-rating mode select, the
/// persisted values after saving and the age-window reason inside the rule diagnosis.
/// </summary>
public sealed class TVSenderAdminTests_AgeWindow
{
    [Fact]
    public async Task AgeWindows_RenderFieldsAndSave()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-agewindow-ui", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _unused) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-agewindow-12"), TimeSpan.FromSeconds(10));

        Assert.NotNull(cut.Find("#tvsender-agewindow-12"));
        Assert.NotNull(cut.Find("#tvsender-agewindow-16"));
        Assert.NotNull(cut.Find("#tvsender-agewindow-18"));
        Assert.NotNull(cut.Find("#tvsender-agewindow-nightend"));
        var modeSelect = cut.Find("#tvsender-unknown-rating");
        Assert.Equal(2, modeSelect.QuerySelectorAll("option").Length);
        Assert.Contains("Standby-Clip", cut.Find("#tvsender-unknown-rating-hint").TextContent);

        cut.Find("#tvsender-agewindow-12").Change("19:30");
        cut.Find("#tvsender-unknown-rating").Change("Unrestricted");
        cut.Find("#tvsender-save-settings").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("gespeichert", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));

        var setup = await fixture.Db.Setups.FirstAsync(ct);
        Assert.Equal(new TimeOnly(19, 30), setup.ProgramScheduleAgeWindow12Local);
        Assert.Equal(ProgramScheduleUnknownRatingMode.Unrestricted, setup.ProgramScheduleUnknownRatingMode);
    }

    [Fact]
    public async Task AgeWindows_InvalidInput_ShowsError()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-agewindow-invalid", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _unused) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-agewindow-12"), TimeSpan.FromSeconds(10));

        // Fsk16 before Fsk12 violates the monotone ordering — the service rejects it and the
        // status line carries the validation message.
        cut.Find("#tvsender-agewindow-16").Change("18:00");
        cut.Find("#tvsender-save-settings").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("aufsteigend", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));

        var setup = await fixture.Db.Setups.FirstAsync(ct);
        Assert.Equal(new TimeOnly(22, 0), setup.ProgramScheduleAgeWindow16Local);
    }

    [Fact]
    public async Task AgeWindows_InvalidTimeInput_NamesFieldAndSavesNothing()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-agewindow-partial", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _unused) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-agewindow-12"), TimeSpan.FromSeconds(10));

        // A parse-failing window time must reject the whole save BEFORE any setting is
        // persisted — the horizon change must not silently go through (partial save).
        cut.Find("#tvsender-horizon-hours").Change("99");
        cut.Find("#tvsender-agewindow-16").Change("keine-zeit");
        cut.Find("#tvsender-save-settings").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("ab 16 Jahre", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));

        var setup = await fixture.Db.Setups.FirstAsync(ct);
        Assert.NotEqual(99, setup.ProgramScheduleHorizonHours);
    }

    [Fact]
    public async Task RuleDiagnosis_ShowsAgeWindowReason()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-agewindow-diag", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var settings = new VideoWebPlayer.Services.ProgramSettingsService(fixture.Db, Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoWebPlayer.Services.ProgramSettingsService>.Instance);
        await settings.UpdateProgramScheduleUnknownRatingModeAsync(ProgramScheduleUnknownRatingMode.RestrictToNightWindow, ct);

        var channel = await fixture.GetChannelAsync(ct);
        var movie = await fixture.CreateMovieAsync("FSK18-Film", duration: TimeSpan.FromMinutes(90), ageRatingMinAge: 18);
        await fixture.CreateRuleAsync(channel.Id, "Mittagsregel", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(12, 0), ProgramRuleMaterialType.Movie, movie.Id);

        var (ctx, _unused) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".tvsender-rule-diagnosis")), TimeSpan.FromSeconds(10));

        var diagnosis = cut.FindAll(".tvsender-rule-diagnosis");
        Assert.Contains(diagnosis, d => d.TextContent.Contains("nicht altersgerecht im Sendefenster")
            || d.TextContent.Contains("kein altersgerechtes Material im Fenster"));
    }
}
