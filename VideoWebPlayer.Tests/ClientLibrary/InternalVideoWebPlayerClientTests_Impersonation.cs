using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.ClientLibrary;

/// <summary>
/// A5 acceptance criterion 4 for the web interface: <see cref="InternalVideoWebPlayerClient"/> obtains the
/// bearer token of the signed-in user for every HTTP verb, not only for GET and POST. Each test uses a
/// fresh client without a token, so the call can only succeed if that client impersonated beforehand.
/// </summary>
public sealed class InternalVideoWebPlayerClientTests_Impersonation : DeviceClientTestBase
{
    [Fact]
    public async Task PagedReadWithCancellationToken_Impersonates()
    {
        var (user, playlistId, _, _) = await ArrangePlaylistAsync("Intern-Seitenabruf");
        var client = CreateInternalClient(user);

        var page = await client.RequestPlaylistEntriesPagedAsync(playlistId, 1, 10, TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task UpdateWithPut_Impersonates()
    {
        var (user, playlistId, _, _) = await ArrangePlaylistAsync("Intern-Umbenennen");
        var client = CreateInternalClient(user);

        var updated = await client.UpdatePlaylistAsync(playlistId, new DtoUpdatePlaylistRequest { Name = "Intern umbenannt", Description = "x" });

        Assert.Equal("Intern umbenannt", updated.Name);
    }

    [Fact]
    public async Task ReorderWithPut_Impersonates()
    {
        var (user, playlistId, firstEntryId, secondEntryId) = await ArrangePlaylistAsync("Intern-Umsortieren");
        var preparation = CreateInternalClient(user);
        await preparation.ChangeSortModeAsync(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.Manual,
            ConfirmLossOfManualOrder = true
        });
        var client = CreateInternalClient(user);

        await client.ReorderPlaylistEntryAsync(playlistId, secondEntryId, new DtoReorderPlaylistEntryRequest { NewSortOrder = 1 });

        var entries = await CreateInternalClient(user).RequestPlaylistEntriesPagedAsync(playlistId, 1, 10, TestContext.Current.CancellationToken);
        Assert.Equal(2, entries.TotalCount);
        Assert.Contains(entries.Entries, e => e.Id == firstEntryId);
    }

    [Fact]
    public async Task SortModeChangeWithPatch_Impersonates()
    {
        var (user, playlistId, _, _) = await ArrangePlaylistAsync("Intern-Sortierung");
        var client = CreateInternalClient(user);

        var updated = await client.ChangeSortModeAsync(playlistId, new DtoChangeSortModeRequest
        {
            NewSortMode = PlaylistSortModeValues.Manual,
            ConfirmLossOfManualOrder = true
        });

        Assert.Equal(PlaylistSortModeValues.Manual, updated.SortMode);
    }

    [Fact]
    public async Task CoverDeletionWithDelete_Impersonates()
    {
        var (user, playlistId, _, _) = await ArrangePlaylistAsync("Intern-Bild");
        var client = CreateInternalClient(user);

        var result = await client.DeletePlaylistCoverAsync(playlistId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task PlaylistDeletionWithDelete_Impersonates()
    {
        var (user, playlistId, _, _) = await ArrangePlaylistAsync("Intern-Loeschen");
        var client = CreateInternalClient(user);

        await client.DeletePlaylistAsync(playlistId);

        Assert.Null(await CreateInternalClient(user).RequestPlaylistAsync(playlistId));
    }

    [Fact]
    public async Task PlaybackNavigation_Impersonates()
    {
        var (user, playlistId, firstEntryId, secondEntryId) = await ArrangePlaylistAsync("Intern-Navigation");

        var next = await CreateInternalClient(user).GetNextPlaylistEntryAsync(playlistId, firstEntryId);
        Assert.Equal(secondEntryId, next!.Entry.Id);

        var previous = await CreateInternalClient(user).GetPreviousPlaylistEntryAsync(playlistId, secondEntryId);
        Assert.Equal(firstEntryId, previous!.Entry.Id);

        var advanced = await CreateInternalClient(user).AdvancePlaylistAsync(playlistId, firstEntryId);
        Assert.Equal(secondEntryId, advanced!.Entry.Id);
    }

    /// <summary>
    /// Creates a fresh circuit-scoped client without an authorization token for the given user, so every
    /// call has to impersonate first.
    /// </summary>
    /// <param name="user">The signed-in user the client acts for.</param>
    /// <returns>The new client instance.</returns>
    private InternalVideoWebPlayerClient CreateInternalClient(ApplicationUser user)
    {
        // The scope has to outlive this method (the client keeps using its UserManager), so the base
        // class holds it and releases it when the test ends.
        var scope = CreateScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id)], "TestAuthentication"));
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };

        return new InternalVideoWebPlayerClient(
            CreateHttpClient(),
            accessor,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<AuthorizationTokenService>(),
            NullLogger<VideoWebPlayerClient>.Instance);
    }

    /// <summary>
    /// Seeds a user with two playable movies and a playlist containing them, created through the
    /// device client of the base class.
    /// </summary>
    /// <param name="playlistName">The name of the playlist to create.</param>
    /// <returns>The user, the playlist id and the ids of its two entries.</returns>
    /// <!-- Tupel-Elemente: User, PlaylistId, FirstEntryId, SecondEntryId -->
    private async Task<(ApplicationUser User, long PlaylistId, long FirstEntryId, long SecondEntryId)> ArrangePlaylistAsync(string playlistName)
    {
        var (user, _) = await CreateUserAndPairDeviceAsync($"intern-{Guid.NewGuid():N}@test.com");
        var (firstMovieId, secondMovieId) = await SeedTwoAccessibleMoviesAsync(user.Id);
        var (playlistId, firstEntryId, secondEntryId) = await CreatePlaylistWithTwoMoviesAsync(playlistName, firstMovieId, secondMovieId);
        return (user, playlistId, firstEntryId, secondEntryId);
    }
}
