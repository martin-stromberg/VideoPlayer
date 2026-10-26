using Bunit;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the "Standby-Clip (Warm-up-Phase)" sub-card of <see cref="TVSenderAdmin"/>:
/// the default chip, the clip picker (source select + candidates), persisting the selection
/// over the settings save button and the server-side admin enforcement of the candidate list.
/// </summary>
public sealed class TVSenderAdminTests_Standby
{
    [Fact]
    public async Task StandbyCard_ShowsDefault_And_PersistsSelection()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-standby", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromSeconds(30));
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => cut.Find("#tvsender-standby-current"), TimeSpan.FromSeconds(10));

        Assert.Contains("Standard-Clip", cut.Find("#tvsender-standby-current").TextContent);

        cut.Find("#tvsender-standby-change").Click();
        cut.WaitForAssertion(
            () => Assert.NotEmpty(cut.FindAll(".standby-candidate-row")), TimeSpan.FromSeconds(10));

        var row = cut.FindAll(".standby-candidate-row")
            .Single(r => r.GetAttribute("data-media-item-id") == item.Id.ToString());
        row.QuerySelector(".standby-candidate-pick")!.Click();
        cut.WaitForAssertion(
            () => Assert.Contains(item.Name, cut.Find("#tvsender-standby-current").TextContent),
            TimeSpan.FromSeconds(5));

        cut.Find("#tvsender-save-settings").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("gespeichert", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));
        Assert.Equal(item.Id, (await fixture.Db.Setups.FirstAsync(ct)).LiveStreamStandbyMediaItemId);

        cut.Find("#tvsender-standby-clear").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("Standard-Clip", cut.Find("#tvsender-standby-current").TextContent),
            TimeSpan.FromSeconds(5));
        cut.Find("#tvsender-save-settings").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("gespeichert", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));
        Assert.Null((await fixture.Db.Setups.FirstAsync(ct)).LiveStreamStandbyMediaItemId);
    }

    [Fact]
    public async Task StandbyCard_OrphanedSelection_SaveStillWorks()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-standby-orphan", ct);
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);
        // Verwaiste Referenz: die gespeicherte Standby-Auswahl zeigt auf ein gelöschtes Medium.
        var setup = await fixture.Db.Setups.FirstAsync(ct);
        setup.LiveStreamStandbyMediaItemId = 424242L;
        await fixture.Db.SaveChangesAsync(ct);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        using var _ctx = ctx;

        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(
            () => Assert.Contains("nicht mehr vorhanden", cut.Find("#tvsender-standby-current").TextContent),
            TimeSpan.FromSeconds(10));

        // Der angezeigte Zustand („Standard-Clip wird verwendet") muss sich speichern
        // lassen — vorher scheiterte jedes „Speichern" an der toten Referenz.
        cut.Find("#tvsender-save-settings").Click();
        cut.WaitForAssertion(
            () => Assert.Contains("gespeichert", cut.Find("#tvsender-channel-status").TextContent),
            TimeSpan.FromSeconds(10));
        Assert.Null((await fixture.Db.Setups.FirstAsync(ct)).LiveStreamStandbyMediaItemId);
        Assert.Empty(cut.FindAll("#tvsender-standby-pending"));
    }

    [Fact]
    public async Task StandbyCard_NonAdmin_Blocked()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-standby-nonadmin", ct);
        var user = await fixture.AddUserAsync("user@test.com", isAdmin: false);

        var service = new ProgramClipGroupService(fixture.Db);
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => service.GetClipCandidatesAsync(user.Id, null, null, 0, 50, ct));
    }
}
