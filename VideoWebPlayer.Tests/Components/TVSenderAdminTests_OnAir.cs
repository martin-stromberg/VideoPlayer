using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the "Sendebetrieb" sub-card of <see cref="TVSenderAdmin"/> (Issue #348):
/// the immediate on-air toggle persists the switch, fires the unbatched worker signal and
/// rolls the checkbox back when the update fails.
/// </summary>
public sealed class TVSenderAdminTests_OnAir
{
    [Fact]
    public async Task SettingsTab_OnAirToggle_PersistsAndNotifies()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-onair-toggle", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;
        var signal = new FakeScheduleSignal();
        ctx.Services.AddSingleton<IProgramScheduleSignal>(signal);

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-on-air"), TimeSpan.FromSeconds(10));

        var toggle = cut.Find("#tvsender-on-air");
        Assert.True(toggle.HasAttribute("checked"));

        toggle.Change(false);
        cut.WaitForAssertion(
            () => Assert.Contains("gestoppt", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));
        Assert.False((await fixture.Db.Setups.FirstAsync(ct)).TvSenderOnAir);
        Assert.True(signal.NowNotifications >= 1, "Der On-Air-Wechsel hat den Worker nicht unverzueglich geweckt.");

        cut.Find("#tvsender-on-air").Change(true);
        cut.WaitForAssertion(
            () => Assert.Contains("auf Sendung", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));
        Assert.True((await fixture.Db.Setups.FirstAsync(ct)).TvSenderOnAir);
    }

    [Fact]
    public async Task SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-onair-fail", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-on-air"), TimeSpan.FromSeconds(10));

        // Persistenz ausser Gefecht: das Update schlaegt fehl — der Schalter muss auf den
        // vorigen Stand zurueckspringen und die Fehlermeldung zeigen.
        await fixture.Db.DisposeAsync();

        cut.Find("#tvsender-on-air").Change(false);
        cut.WaitForAssertion(
            () => Assert.False(string.IsNullOrWhiteSpace(cut.Find("#tvsender-channel-status").TextContent)),
            TimeSpan.FromSeconds(10));

        var toggle = cut.Find("#tvsender-on-air");
        Assert.True(toggle.HasAttribute("checked"),
            "Die Checkbox ist nach dem fehlgeschlagenen Update nicht zurueckgesprungen.");
    }
}
