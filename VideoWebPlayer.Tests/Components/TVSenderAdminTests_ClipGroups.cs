using Bunit;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for the "Clip-Gruppen" tab and the clip-group fields of the rule editor on
/// <see cref="TVSenderAdmin"/>: tab visibility, group CRUD surface, member duration labels,
/// rule multi-select + grace field and the material-label suffix.
/// </summary>
public sealed class TVSenderAdminTests_ClipGroups
{
    private static async Task<(BunitContext Ctx, IRenderedComponent<TVSenderAdmin> Cut)> RenderAsync(
        ProgramScheduleTestDb fixture)
    {
        var admin = await fixture.AddUserAsync("admin@t.t", isAdmin: true);
        var (ctx, _) = await TVSenderAdminTests.CreateContextAsync(fixture, admin.Id);
        var cut = ctx.Render<TVSenderAdmin>();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".admin-tab")), TimeSpan.FromSeconds(10));
        return (ctx, cut);
    }

    private static void OpenClipGroupsTab(IRenderedComponent<TVSenderAdmin> cut)
    {
        cut.Find("#tvsender-tab-clipgruppen").Click();
        cut.WaitForAssertion(() => Assert.Contains("active", cut.Find("#tab-clipgruppen").ClassList), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ClipGroupsTab_Visible_AndShowsSystemTrailerRow()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("bunit-cg-tab", ct);
        var (ctx, cut) = await RenderAsync(fixture);
        using var _ = ctx;

        OpenClipGroupsTab(cut);
        var trailerRow = cut.FindAll(".clipgroup-row").Single(r => r.TextContent.Contains("Trailer"));
        Assert.Empty(trailerRow.QuerySelectorAll(".clipgroup-delete"));
        Assert.Empty(trailerRow.QuerySelectorAll(".clipgroup-rename"));
    }

    [Fact]
    public async Task ClipGroupsTab_CreateGroup_RendersRow()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("bunit-cg-create", ct);
        var (ctx, cut) = await RenderAsync(fixture);
        using var _ = ctx;

        OpenClipGroupsTab(cut);
        cut.Find("#clipgroup-new").Click();
        cut.Find("#clipgroup-name").Change("Maskottchen");
        cut.Find("#clipgroup-save").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll(".clipgroup-row"), r => r.TextContent.Contains("Maskottchen")), TimeSpan.FromSeconds(10));
        Assert.NotNull(await fixture.Db.ProgramClipGroups.FirstOrDefaultAsync(g => g.Name == "Maskottchen", ct));
    }

    [Fact]
    public async Task MemberList_MarksItemWithoutDuration()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("bunit-cg-nodur", ct);
        var group = await fixture.CreateClipGroupAsync("G", ct);
        var item = await fixture.CreateMediaItemAsync(1, null);
        await fixture.AddClipToGroupAsync(group.Id, item.Id, ct);
        var (ctx, cut) = await RenderAsync(fixture);
        using var _ = ctx;

        OpenClipGroupsTab(cut);
        cut.FindAll(".clipgroup-manage").Single(b => b.TextContent.Contains("verwalten")).Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("keine Laufzeit", cut.Find("#clipgroup-members-card").TextContent), TimeSpan.FromSeconds(10));
        // Der Hinweis erklärt die Konsequenz: ohne Laufzeit wird der Clip nicht verwendet.
        Assert.Contains("nicht berücksichtigt", cut.Find("#clipgroup-noduration-hint").TextContent);
    }

    [Fact]
    public async Task CandidateList_Empty_ExplainsSelectionRule()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("bunit-cg-empty", ct);
        await fixture.CreateClipGroupAsync("G", ct);
        var (ctx, cut) = await RenderAsync(fixture);
        using var _ = ctx;

        OpenClipGroupsTab(cut);
        cut.FindAll(".clipgroup-manage").Single(b => b.TextContent.Contains("verwalten")).Click();

        // Statt einer leeren Tabelle erklärt der Leerstand die Auswahlregel.
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("#clip-candidate-empty")), TimeSpan.FromSeconds(10));
        var empty = cut.Find("#clip-candidate-empty");
        Assert.Contains("Keine zuordenbaren Clips gefunden", empty.TextContent);
        Assert.Contains("Videodateien ohne Film-/Serien-Zuordnung", empty.TextContent);
        Assert.Empty(cut.FindAll(".clip-candidate-row"));
    }

    [Fact]
    public async Task RuleEditor_ShowsClipGroupSelection_GraceField_AndMaterialLabelSuffix()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("bunit-cg-rule", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var group = await fixture.CreateClipGroupAsync("Maskottchen", ct);
        var movie = await fixture.CreateMovieAsync("Film", 1, TimeSpan.FromMinutes(90));
        await fixture.CreateRuleAsync(channel.Id, "Primetime", ProgramRuleType.MoviePrimeTime,
            ProgramRuleRecurrence.Daily, new TimeOnly(20, 0), ProgramRuleMaterialType.Movie, movie.Id,
            slotDuration: TimeSpan.FromHours(4), clipGroupIds: [group.Id]);
        var (ctx, cut) = await RenderAsync(fixture);
        using var _ = ctx;

        // MaterialLabel-Suffix in der Regel-Tabelle.
        Assert.Contains("Clips: Maskottchen", cut.Markup);

        cut.Find("#tvsender-new-rule").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#rule-editor")), TimeSpan.FromSeconds(10));
        Assert.NotEmpty(cut.FindAll("#rule-clip-groups"));
        Assert.Equal(2, cut.FindAll(".rule-clip-group-option").Count); // Trailer + Maskottchen

        // Ohne Sendeplatz-Länge bleibt das Karenzzeit-Feld ausgeblendet; der Hinweis
        // erklärt die Kopplung an die Länge. Erst mit Länge erscheint das Feld.
        Assert.Empty(cut.FindAll("#rule-grace-minutes"));
        Assert.NotEmpty(cut.FindAll("#rule-clip-groups-hint"));
        cut.Find("#rule-duration").Change("240");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#rule-grace-minutes")), TimeSpan.FromSeconds(10));
    }
}
