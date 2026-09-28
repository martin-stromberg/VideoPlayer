using System.Net;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// A6, Teil 4: zwei Anwender mit je einem eigenen, per QR-Bootstrap gekoppelten Gerät. Das Gerät des
/// zweiten Anwenders sieht ausschließlich dessen eigene Playlists und die öffentlichen — die private
/// Playlist des ersten Anwenders weder in der Liste noch einzeln, und ändern darf es weder die private noch
/// die öffentliche des anderen.
/// </summary>
[Trait("Category", "E2E")]
public sealed class DevicePlaylistE2ETests_MultiUser : DeviceClientTestBase
{
    [Fact]
    public async Task SecondUserDevice_SeesOnlyOwnAndPublicPlaylists()
    {
        var fixture = await PairTwoDevicesAsync();

        var ownPlaylists = (await fixture.DeviceB.RequestPlaylistsAsync()).ToList();
        Assert.Contains(ownPlaylists, p => p.Id == fixture.PrivateOfB);
        Assert.DoesNotContain(ownPlaylists, p => p.Id == fixture.PrivateOfA);
        Assert.DoesNotContain(ownPlaylists, p => p.Id == fixture.PublicOfA);

        var publicPlaylists = (await fixture.DeviceB.RequestPublicPlaylistsAsync()).ToList();
        var publicOfA = Assert.Single(publicPlaylists, p => p.Id == fixture.PublicOfA);
        Assert.False(publicOfA.IsOwner);
        Assert.DoesNotContain(publicPlaylists, p => p.Id == fixture.PrivateOfA);
    }

    [Fact]
    public async Task SecondUserDevice_RequestsForeignPrivatePlaylist_IsRefused()
    {
        var fixture = await PairTwoDevicesAsync();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.DeviceB.RequestPlaylistAsync(fixture.PrivateOfA));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task SecondUserDevice_AddsEntryToForeignPrivatePlaylist_IsRefused()
    {
        var fixture = await PairTwoDevicesAsync();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.DeviceB.AddMediaToPlaylistAsync(
            fixture.PrivateOfA, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = fixture.MovieOfB }));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task SecondUserDevice_AddsEntryToForeignPublicPlaylist_IsRefused()
    {
        var fixture = await PairTwoDevicesAsync();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => fixture.DeviceB.AddMediaToPlaylistAsync(
            fixture.PublicOfA, new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = fixture.MovieOfB }));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    /// <summary>
    /// The starting point of every test here: user A (an administrator, so they may mark one of their own
    /// playlists public) with a paired device on <see cref="DeviceClientTestBase.Client"/>, and user B with a
    /// second, independently paired device — plus one private playlist each and one public playlist of A.
    /// </summary>
    /// <returns>The second device's client and the three playlist ids.</returns>
    private async Task<TwoDeviceFixture> PairTwoDevicesAsync()
    {
        var userA = await CreateUserAsync($"geraet-a-{Guid.NewGuid():N}@test.com", isAdmin: true);
        await PairDeviceAsync(await CreateBootstrapTicketAsync(userA.Id), "Gerät A");
        var (firstMovieOfA, secondMovieOfA) = await SeedTwoAccessibleMoviesAsync(userA.Id);
        var (privateOfA, _, _) = await CreatePlaylistWithTwoMoviesAsync("A-Privat", firstMovieOfA, secondMovieOfA);
        var (publicOfA, _, _) = await CreatePlaylistWithTwoMoviesAsync("A-Oeffentlich", firstMovieOfA, secondMovieOfA);
        var published = await Client.SetPlaylistPublicAsync(publicOfA, new DtoSetPlaylistPublicRequest { IsPublic = true });
        Assert.True(published.IsPublic);

        var userB = await CreateUserAsync($"geraet-b-{Guid.NewGuid():N}@test.com");
        var deviceB = CreateRecordedClient().Client;
        await PairDeviceAsync(deviceB, await CreateBootstrapTicketAsync(userB.Id), "Gerät B");
        var (firstMovieOfB, secondMovieOfB) = await SeedTwoAccessibleMoviesAsync(userB.Id);
        var playlistOfB = await deviceB.CreatePlaylistAsync(new DtoCreatePlaylistRequest { Name = "B-Privat", SortMode = "ByReleaseDate" });
        await deviceB.AddMediaToPlaylistAsync(playlistOfB.Id,
            new DtoAddMediaToPlaylistRequest { MediaType = MediaTypeValues.Movie, MediaId = firstMovieOfB });

        return new TwoDeviceFixture
        {
            DeviceB = deviceB,
            PrivateOfA = privateOfA,
            PublicOfA = publicOfA,
            PrivateOfB = playlistOfB.Id,
            MovieOfB = secondMovieOfB
        };
    }

    /// <summary>
    /// The prepared two-user, two-device situation shared by the tests of this class.
    /// </summary>
    private sealed class TwoDeviceFixture
    {
        /// <summary>The client of the second user's device.</summary>
        public required VideoWebPlayer.Client.VideoWebPlayerClient DeviceB { get; init; }

        /// <summary>The id of the first user's private playlist.</summary>
        public required long PrivateOfA { get; init; }

        /// <summary>The id of the first user's public playlist.</summary>
        public required long PublicOfA { get; init; }

        /// <summary>The id of the second user's private playlist.</summary>
        public required long PrivateOfB { get; init; }

        /// <summary>The id of a movie the second user may access, for attempted foreign modifications.</summary>
        public required long MovieOfB { get; init; }
    }
}
