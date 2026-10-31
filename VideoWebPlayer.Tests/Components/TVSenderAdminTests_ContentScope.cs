using Bunit;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the "Inhaltsspektrum" selection of the "Planung" sub-card of
/// <see cref="TVSenderAdmin"/>: the two named options, the restrictive default, the
/// consequence hint and the persisted value after saving via the settings save button.
/// </summary>
public sealed class TVSenderAdminTests_ContentScope
{
    [Fact]
    public async Task ContentScope_RendersOptionsAndHint_DefaultsToSharedOnly()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-scope-render", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-content-scope"), TimeSpan.FromSeconds(10));

        var select = cut.Find("#tvsender-content-scope");
        var options = select.QuerySelectorAll("option");
        Assert.Equal(2, options.Length);
        Assert.Contains("Nur für alle Benutzer freigegebene Inhalte", options[0].TextContent);
        Assert.Contains("Alle Inhalte, die sich aus den Regeln ergeben", options[1].TextContent);
        // Die restriktive Variante ist der ausgelieferte Default.
        Assert.Equal("SharedOnly", select.GetAttribute("value"));

        var hint = cut.Find("#tvsender-content-scope-hint");
        Assert.Contains("Mediathek", hint.TextContent);
    }

    [Fact]
    public async Task ContentScope_SavePersistsSelection()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-scope-save", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-content-scope"), TimeSpan.FromSeconds(10));

        cut.Find("#tvsender-content-scope").Change("AllContent");
        cut.Find("#tvsender-save-settings").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("gespeichert", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));

        Assert.Equal(
            ProgramScheduleContentScope.AllContent,
            (await fixture.Db.Setups.FirstAsync(ct)).ProgramScheduleContentScope);
    }
}
