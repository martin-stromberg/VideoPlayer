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

        // Die Clips-Spalte der System-Gruppe sagt „automatisch" — eine 0 würde die
        // dynamisch befüllte Gruppe fälschlich als leer ausweisen.
        Assert.Equal("automatisch", trailerRow.QuerySelectorAll("td")[1].TextContent.Trim());
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
    public async Task MemberList_FiltersLive_BySearchTerm()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("bunit-cg-membersearch", ct);
        var group = await fixture.CreateClipGroupAsync("G", ct);
        var clipA = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        clipA.Name = "funki-werbung.mkv";
        var clipB = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        clipB.Name = "trenner.mkv";
        await fixture.SaveDiagAsync();
        await fixture.AddClipToGroupAsync(group.Id, clipA.Id, ct);
        await fixture.AddClipToGroupAsync(group.Id, clipB.Id, ct);
        var (ctx, cut) = await RenderAsync(fixture);
        using var _ = ctx;

        OpenClipGroupsTab(cut);
        cut.FindAll(".clipgroup-manage").Single(b => b.TextContent.Contains("verwalten")).Click();
        cut.WaitForAssertion(() =>
            Assert.Equal(2, cut.FindAll(".clipgroup-member-row").Count), TimeSpan.FromSeconds(10));

        // Clientseitige Live-Filterung der geladenen Mitglieder (Muster der Kandidaten-Suche).
        cut.Find("#clip-member-search").Input("funki");
        var rows = cut.FindAll(".clipgroup-member-row");
        Assert.Single(rows);
        Assert.Contains("funki-werbung.mkv", rows[0].TextContent);

        // Ohne Treffer erklärt ein Hinweis den leeren Tabellenkörper.
        cut.Find("#clip-member-search").Input("nix-gefunden");
        Assert.Empty(cut.FindAll(".clipgroup-member-row"));
        Assert.NotEmpty(cut.FindAll("#clip-member-search-empty"));
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
    public async Task CandidateSearch_FiltersLive_WithoutSearchButton()
    {
        var ct = Xunit.TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("bunit-cg-livesearch", ct);
        await fixture.CreateClipGroupAsync("G", ct);
        var item = await fixture.CreateMediaItemAsync(1, TimeSpan.FromMinutes(1));
        var (ctx, cut) = await RenderAsync(fixture);
        using var _ = ctx;

        OpenClipGroupsTab(cut);
        cut.FindAll(".clipgroup-manage").Single(b => b.TextContent.Contains("verwalten")).Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(cut.FindAll(".clip-candidate-row"), r => r.TextContent.Contains(item.Name)),
            TimeSpan.FromSeconds(10));

        // Live-Suche (Debounce wie MediaSearchSelector): der Filter greift nach dem Tippen
        // von allein — es gibt keine „Suchen"-Schaltfläche mehr.
        Assert.Empty(cut.FindAll("#clip-candidate-refresh"));
        cut.Find("#clip-candidate-search").Input("begriff-ohne-treffer");
        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("#clip-candidate-empty")), TimeSpan.FromSeconds(10));
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

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".tvsender-rule-row")), TimeSpan.FromSeconds(10));
        var row = cut.FindAll(".tvsender-rule-row").Single(r => r.TextContent.Contains("Primetime"));
        Assert.Contains("1 Clip-Gruppe(n)", row.TextContent);
        Assert.DoesNotContain("Clips: Maskottchen", row.TextContent);

        row.QuerySelector(".tvsender-rule-edit")!.Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#rule-editor")), TimeSpan.FromSeconds(10));
        Assert.Contains("Clips: Maskottchen", cut.Find("#rule-material-selected").TextContent);
        cut.FindAll("#rule-editor button").Single(b => b.TextContent.Contains("Schließen")).Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#rule-editor")), TimeSpan.FromSeconds(10));

        cut.Find("#tvsender-new-rule").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#rule-editor")), TimeSpan.FromSeconds(10));
        Assert.NotEmpty(cut.FindAll("#rule-clip-groups"));
        Assert.Equal(2, cut.FindAll(".rule-clip-group-option").Count); // Trailer + Maskottchen

        // Ohne Sendeplatz-Länge bleibt das Karenzzeit-Feld ausgeblendet; der Hinweis
        // erklärt die Kopplung an die Länge.
        Assert.Empty(cut.FindAll("#rule-grace-minutes"));
        Assert.Contains("Länge", cut.Find("#rule-clip-groups-hint").TextContent);

        // Mit Länge, aber ohne gewählte Gruppe bleibt das Feld wirkungslos — der Hinweis
        // erklärt, dass die Karenzzeit eine Clip-Gruppe braucht.
        cut.Find("#rule-duration").Change("240");
        cut.WaitForAssertion(() =>
            Assert.Contains("Clip-Gruppe", cut.Find("#rule-clip-groups-hint").TextContent), TimeSpan.FromSeconds(10));
        Assert.Empty(cut.FindAll("#rule-grace-minutes"));

        // Erst mit gewählter Gruppe erscheint das Feld samt Hinweis auf seine Wirkung.
        cut.Find($"#rule-clip-group-{group.Id}").Change(true);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#rule-grace-minutes")), TimeSpan.FromSeconds(10));
        Assert.Contains("Restzeit", cut.Find("#rule-grace-minutes-hint").TextContent);
    }
}
