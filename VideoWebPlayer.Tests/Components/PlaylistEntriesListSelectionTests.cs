using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Tests for the entry selection and the two separated content areas ("Titel der Playlist" / "Titel
/// hinzufügen") of <see cref="PlaylistEntriesList"/> (Kundenrückmeldung zur Detailansicht, D8/D9): a click
/// selects an entry (marked, keyboard operable, clearable), the tiles carry no play/remove buttons, the mode
/// toggle exists for the owner only, an empty playlist starts in the add mode, a non-empty one in the list
/// mode.
/// </summary>
public class PlaylistEntriesListSelectionTests
{
    // ----- Selection (D8) -------------------------------------------------------------------------------

    [Fact]
    public void Tiles_HaveOptionSemantics_AndNoPlayOrRemoveButtons()
    {
        var (cut, _, _) = Render(entries: Movies(2));

        var list = cut.Find(".playlist-entries-list");
        Assert.Equal("listbox", list.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(list.GetAttribute("aria-label")));
        foreach (var row in cut.FindAll(".playlist-entry-row"))
        {
            Assert.Equal("option", row.GetAttribute("role"));
            Assert.Equal("0", row.GetAttribute("tabindex"));
            Assert.Equal("false", row.GetAttribute("aria-selected"));
            Assert.Empty(row.QuerySelectorAll("button.playlist-entry-play-button"));
            Assert.Empty(row.QuerySelectorAll("button.playlist-entry-remove-button"));
        }
    }

    [Fact]
    public async Task ClickOnTile_RaisesOnEntrySelectedWithThatEntry()
    {
        var (cut, selections, _) = Render(entries: Movies(3));

        await cut.InvokeAsync(() => cut.FindAll(".playlist-entry-row")[1].ClickAsync(new MouseEventArgs()));

        var selected = Assert.Single(selections);
        Assert.NotNull(selected);
        Assert.Equal(11, selected!.Id);
    }

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public async Task KeyboardEnterOrSpace_SelectsTheFocusedEntry(string key)
    {
        var (cut, selections, _) = Render(entries: Movies(2));

        await cut.InvokeAsync(() => cut.FindAll(".playlist-entry-row")[0].KeyDownAsync(new KeyboardEventArgs { Key = key }));

        Assert.Equal(10, Assert.Single(selections)!.Id);
    }

    [Fact]
    public async Task OtherKeys_DoNotSelect()
    {
        var (cut, selections, _) = Render(entries: Movies(2));

        await cut.InvokeAsync(() => cut.FindAll(".playlist-entry-row")[0].KeyDownAsync(new KeyboardEventArgs { Key = "a" }));
        await cut.InvokeAsync(() => cut.FindAll(".playlist-entry-row")[0].KeyDownAsync(new KeyboardEventArgs { Key = "Tab" }));

        Assert.Empty(selections);
    }

    [Fact]
    public void SelectedEntry_IsMarked_WithSelectedClassAndAriaSelected_OthersAreNot()
    {
        var (cut, _, _) = Render(entries: Movies(3), selectedEntryId: 11);

        var rows = cut.FindAll(".playlist-entry-row");
        Assert.Contains("selected", rows[1].ClassList);
        Assert.Equal("true", rows[1].GetAttribute("aria-selected"));
        Assert.DoesNotContain("selected", rows[0].ClassList);
        Assert.Equal("false", rows[0].GetAttribute("aria-selected"));
        Assert.Equal("false", rows[2].GetAttribute("aria-selected"));
    }

    [Fact]
    public async Task Escape_ClearsTheSelection()
    {
        var (cut, selections, _) = Render(entries: Movies(2), selectedEntryId: 10);

        await cut.InvokeAsync(() => cut.FindAll(".playlist-entry-row")[0].KeyDownAsync(new KeyboardEventArgs { Key = "Escape" }));

        Assert.Null(Assert.Single(selections));
    }

    [Fact]
    public void ManualMode_KeepsReorderControls_ButStillNoPlayOrRemoveButtons()
    {
        var (cut, _, _) = Render(entries: Movies(2), isManualMode: true);

        var row = cut.FindAll(".playlist-entry-row")[0];
        Assert.Equal("true", row.GetAttribute("draggable"));
        Assert.Single(row.QuerySelectorAll(".playlist-entry-move-start-button"));
        Assert.Single(row.QuerySelectorAll(".playlist-entry-move-end-button"));
        Assert.All(row.QuerySelectorAll(".playlist-entry-actions button"), b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(b.GetAttribute("title")));
            Assert.Equal(b.GetAttribute("title"), b.GetAttribute("aria-label"));
        });
        Assert.Empty(row.QuerySelectorAll("button.playlist-entry-play-button"));
        Assert.Empty(row.QuerySelectorAll("button.playlist-entry-remove-button"));
    }

    [Fact]
    public async Task ClickOnReorderButton_DoesNotSelectTheEntry()
    {
        var (cut, selections, mock) = Render(entries: Movies(2), isManualMode: true);
        mock.Setup(c => c.MoveEntryToBeginningAsync(1, It.IsAny<long>())).ReturnsAsync(new DtoPlaylistEntry());

        await cut.InvokeAsync(() => cut.FindAll(".playlist-entry-move-start-button")[1].ClickAsync(new MouseEventArgs()));

        Assert.Empty(selections);
        mock.Verify(c => c.MoveEntryToBeginningAsync(1, 11), Times.Once);
    }

    // ----- Mode (D9) -------------------------------------------------------------------------------------

    [Fact]
    public void EmptyPlaylist_Owner_StartsInAddMode()
    {
        var (cut, _, _) = Render(entries: Array.Empty<DtoPlaylistEntry>());

        Assert.Single(cut.FindComponents<MediaSearchSelector>());
        Assert.True(Assert.Single(ReportedModes));
        Assert.Empty(cut.FindAll(".playlist-entries-list"));
    }

    [Fact]
    public void NonEmptyPlaylist_Owner_StartsInListMode_AddModeIsSeparate()
    {
        var (cut, _, _) = Render(entries: Movies(2));

        Assert.False(Assert.Single(ReportedModes));
        Assert.Equal(2, cut.FindAll(".playlist-entry-row").Count);
        // The two areas are separated: no search field next to the list.
        Assert.Empty(cut.FindComponents<MediaSearchSelector>());
        Assert.Empty(cut.FindAll("#playlist-add-area"));
    }

    /// <summary>
    /// The mode toggle moved to the header (Kundenrückmeldung): the content area itself must not render any
    /// switch of its own any more.
    /// </summary>
    [Fact]
    public void ContentArea_HasNoModeToggleOfItsOwnAnyMore()
    {
        var (cut, _, _) = Render(entries: Movies(1));

        Assert.Empty(cut.FindAll("#playlist-content-mode-group"));
        Assert.Empty(cut.FindAll(".playlist-mode-button"));
        Assert.Empty(cut.FindAll("#playlist-mode-add-button"));
        Assert.Empty(cut.FindAll("#playlist-mode-entries-button"));
    }

    [Fact]
    public async Task SwitchingModes_ShowsExactlyOneAreaAtATime_AndReportsTheNewOne()
    {
        var (cut, _, _) = Render(entries: Movies(2));

        await cut.InvokeAsync(() => cut.Instance.ToggleModeAsync());

        Assert.Single(cut.FindComponents<MediaSearchSelector>());
        Assert.Empty(cut.FindAll(".playlist-entry-row"));
        Assert.Equal(new[] { false, true }, ReportedModes);

        await cut.InvokeAsync(() => cut.Instance.ToggleModeAsync());

        Assert.Empty(cut.FindComponents<MediaSearchSelector>());
        Assert.Equal(2, cut.FindAll(".playlist-entry-row").Count);
        Assert.Equal(new[] { false, true, false }, ReportedModes);
    }

    [Fact]
    public async Task SwitchingToAddMode_ClearsTheSelection()
    {
        var (cut, selections, _) = Render(entries: Movies(2), selectedEntryId: 10);

        await cut.InvokeAsync(() => cut.Instance.ToggleModeAsync());

        Assert.Null(Assert.Single(selections));
    }

    /// <summary>
    /// Read-only (a public playlist of somebody else): the mode never leaves the title list, so the header is
    /// never told to offer a toggle and no add area is rendered.
    /// </summary>
    [Fact]
    public async Task ReadOnly_NeverSwitchesToTheAddArea_OnlyTheList()
    {
        var (cut, _, _) = Render(entries: Movies(2), isReadOnly: true);

        await cut.InvokeAsync(() => cut.Instance.ToggleModeAsync());

        Assert.Empty(ReportedModes);
        Assert.Empty(cut.FindAll("#playlist-add-area"));
        Assert.Empty(cut.FindComponents<MediaSearchSelector>());
        Assert.Equal(2, cut.FindAll(".playlist-entry-row").Count);
    }

    [Fact]
    public void ReadOnly_EmptyPlaylist_ShowsEmptyState_NeverTheAddArea()
    {
        var (cut, _, _) = Render(entries: Array.Empty<DtoPlaylistEntry>(), isReadOnly: true);

        Assert.Empty(cut.FindComponents<MediaSearchSelector>());
        Assert.Contains("Keine Einträge vorhanden", cut.Find(".admin-empty-state").TextContent);
        Assert.DoesNotContain("hinzufügen", cut.Find(".admin-empty-state").TextContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddingTitles_StaysInAddMode_SoSeveralTitlesCanBeAddedInARow()
    {
        var (cut, _, mock) = Render(entries: Array.Empty<DtoPlaylistEntry>());
        mock.Setup(c => c.AddMediaToPlaylistAsync(1, It.IsAny<DtoAddMediaToPlaylistRequest>()))
            .ReturnsAsync(new DtoPlaylistAddResult { Message = "1 Titel hinzugefügt." });

        // After the first add the (mocked) server-side list now has an entry: the mode must not flip to the list.
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Movies(1), HasNextPage = false, TotalCount = 1 });
        await cut.InvokeAsync(() => cut.FindComponent<MediaSearchSelector>().Instance.OnMediaSelected.InvokeAsync(("Movie", 1L)));
        await cut.InvokeAsync(() => cut.FindComponent<MediaSearchSelector>().Instance.OnMediaSelected.InvokeAsync(("Movie", 2L)));

        Assert.Single(cut.FindComponents<MediaSearchSelector>());
        Assert.True(ReportedModes[^1]);
        mock.Verify(c => c.AddMediaToPlaylistAsync(1, It.IsAny<DtoAddMediaToPlaylistRequest>()), Times.Exactly(2));
        // The success message of the add stays visible.
        Assert.Contains("hinzugefügt", cut.Find("#playlist-entries-status").TextContent);
    }

    [Fact]
    public async Task RemovingTheLastTitle_SwitchesToAddMode_AndReportsTheRemoval()
    {
        var entry = Movies(1)[0];
        var removed = new List<DtoPlaylistEntry>();
        var (cut, _, mock) = Render(entries: new[] { entry }, onEntryRemoved: removed.Add);
        mock.Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", entry.MediaId, false)).Returns(Task.CompletedTask);
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });
        Assert.False(Assert.Single(ReportedModes));

        await cut.InvokeAsync(() => cut.Instance.RemoveEntryAsync(entry));

        Assert.Equal(entry.Id, Assert.Single(removed).Id);
        Assert.Single(cut.FindComponents<MediaSearchSelector>());
        Assert.True(ReportedModes[^1]);
    }

    [Fact]
    public async Task RemovingOneOfSeveralTitles_StaysInListMode()
    {
        var entries = Movies(2);
        var (cut, _, mock) = Render(entries: entries);
        mock.Setup(c => c.RemoveMediaFromPlaylistAsync(1, "Movie", entries[0].MediaId, false)).Returns(Task.CompletedTask);
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(1, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = new[] { entries[1] }, HasNextPage = false, TotalCount = 1 });

        await cut.InvokeAsync(() => cut.Instance.RemoveEntryAsync(entries[0]));

        Assert.Empty(cut.FindComponents<MediaSearchSelector>());
        Assert.Single(cut.FindAll(".playlist-entry-row"));
    }

    [Fact]
    public void OnlyCollectionEntries_CountAsEmpty_AndOfferTheAddMode()
    {
        // Organizational entries (a whole TV show / season) get no tile: nothing to list, so adding is offered.
        var (cut, _, _) = Render(entries: new[]
        {
            new DtoPlaylistEntry { Id = 1, PlaylistId = 1, MediaType = "TVShow", MediaId = 5, MediaTitle = "Serie", IsAccessible = true }
        });

        Assert.Single(cut.FindComponents<MediaSearchSelector>());
    }

    // ----- helpers ---------------------------------------------------------------------------------------

    private static DtoPlaylistEntry[] Movies(int count)
        => Enumerable.Range(0, count)
            .Select(i => new DtoPlaylistEntry
            {
                Id = 10 + i,
                PlaylistId = 1,
                MediaType = "Movie",
                MediaId = 100 + i,
                MediaTitle = $"Film {i}",
                IsAccessible = true,
                SortOrder = i
            })
            .ToArray();

    // Collects every content area reported through PlaylistEntriesList.OnModeChanged - the mode toggle itself
    // lives in the header of PlaylistDetail, so the mode is only observable here.
    private static readonly List<bool> ReportedModes = new();

    private static (IRenderedComponent<PlaylistEntriesList> Cut, List<DtoPlaylistEntry?> Selections, Mock<IPlaylistApiClient> Mock) Render(
        DtoPlaylistEntry[] entries,
        long? selectedEntryId = null,
        bool isManualMode = false,
        bool isReadOnly = false,
        Action<DtoPlaylistEntry>? onEntryRemoved = null)
    {
        ReportedModes.Clear();
        var ctx = new global::Bunit.BunitContext();
        var mock = new Mock<IPlaylistApiClient>();
        mock.Setup(c => c.RequestPlaylistEntriesPagedAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = entries, HasNextPage = false, TotalCount = entries.Length });

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(mock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);

        var selections = new List<DtoPlaylistEntry?>();
        var cut = ctx.Render<PlaylistEntriesList>(parameters =>
        {
            parameters
                .Add(p => p.PlaylistId, 1)
                .Add(p => p.IsManualMode, isManualMode)
                .Add(p => p.IsReadOnly, isReadOnly)
                .Add(p => p.SelectedEntryId, selectedEntryId)
                .Add(p => p.OnEntrySelected, EventCallback.Factory.Create<DtoPlaylistEntry?>(new object(), e => selections.Add(e)))
                .Add(p => p.OnModeChanged, EventCallback.Factory.Create<bool>(new object(), ReportedModes.Add));
            if (onEntryRemoved is not null)
                parameters.Add(p => p.OnEntryRemoved, EventCallback.Factory.Create<DtoPlaylistEntry>(new object(), onEntryRemoved));
        });

        return (cut, selections, mock);
    }
}
