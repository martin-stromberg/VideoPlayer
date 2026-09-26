using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// End-to-end tests for the final error answers of the media API on the playlist detail page (A4):
/// a viewer of somebody else's public playlist who addresses a title that is not unlocked for them
/// reads a plain message, and a title that does not exist any more says so — in both cases the page
/// stays usable and no sign-in is triggered.
/// </summary>
[Trait("Category", "E2E")]
public sealed class PlaylistForbiddenEntryE2ETests : PlaylistsE2ETestBase
{
    [Fact]
    public async Task ForeignPublicPlaylist_LockedEntry_ShowsNoAccessMessage_AndNoSignIn()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Oeffentliche-Playlist-Gesperrter-Titel");
        var lockedMovieId = await SeedLockedMovieIntoPlaylistAsync("Oeffentliche-Playlist-Gesperrter-Titel", "Gesperrter-Fremdtitel");
        await MakePlaylistPublicAsync("Oeffentliche-Playlist-Gesperrter-Titel");
        var (playlistId, entryId) = await GetPlaylistAndEntryIdAsync("Oeffentliche-Playlist-Gesperrter-Titel", MediaTypeValues.Movie, lockedMovieId);

        await LoginAsync(UserBEmail);
        await Page.GotoAsync($"{ServerUrl}/playlists/{playlistId}?entryId={entryId}");
        await Page.WaitForSelectorAsync("#playlist-playback-error");

        await Expect(Page.Locator("#playlist-playback-error")).ToHaveTextAsync("Sie haben keinen Zugriff auf diesen Titel.");
        // The playlist itself stays visible and no sign-in page is shown.
        await Expect(Page.Locator("#playlist-detail-name")).ToBeVisibleAsync();
        Assert.DoesNotContain("/Account/Login", Page.Url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OwnPlaylist_UnknownEntry_ShowsNotFoundMessage()
    {
        if (SkipBrowser)
            return;

        await LoginAsync(UserAEmail);
        await CreatePlaylistViaUiAsync("Playlist-Unbekannter-Eintrag");
        var movieId = await SeedMovieWithPosterIntoPlaylistAsync("Playlist-Unbekannter-Eintrag", "Vorhandener-Titel");
        var (playlistId, entryId) = await GetPlaylistAndEntryIdAsync("Playlist-Unbekannter-Eintrag", MediaTypeValues.Movie, movieId);

        await Page.GotoAsync($"{ServerUrl}/playlists/{playlistId}?entryId={entryId + 100000}");
        await Page.WaitForSelectorAsync("#playlist-playback-error");

        await Expect(Page.Locator("#playlist-playback-error")).ToHaveTextAsync("Dieser Titel existiert nicht oder hat keine Videodatei.");
        await Expect(Page.Locator("#playlist-detail-name")).ToBeVisibleAsync();
    }
}
