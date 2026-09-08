using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Playlists;
using VideoWebPlayer.Controllers.Models;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Integration test covering the wiring between <see cref="MediaSearchSelector"/> and
/// <see cref="PlaylistEntriesList"/>: a search-result selection must reach
/// <c>IPlaylistApiClient.AddMediaToPlaylistAsync</c> via <c>PlaylistEntriesList.OnMediaSelectedAsync</c>
/// and <c>AddEntryAsync</c>, exactly as the removed dropdown + id-input previously did.
/// </summary>
public class PlaylistEntriesListTests
{
    [Fact]
    public async Task PlaylistEntriesList_OnMediaSelectedAsync_CallsAddEntryAsync()
    {
        using var ctx = new global::Bunit.TestContext();

        var playlistClientMock = new Mock<IPlaylistApiClient>();
        playlistClientMock
            .Setup(c => c.RequestPlaylistEntriesPagedAsync(It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DtoPlaylistEntriesPagedResult { Entries = Array.Empty<DtoPlaylistEntry>(), HasNextPage = false, TotalCount = 0 });
        playlistClientMock
            .Setup(c => c.AddMediaToPlaylistAsync(It.IsAny<long>(), It.IsAny<DtoAddMediaToPlaylistRequest>()))
            .ReturnsAsync(new DtoPlaylistAddResult { Message = "1 Titel hinzugefuegt." });

        ctx.Services.AddSingleton<VideoWebPlayerClient>(new NoOpVideoWebPlayerClient());
        ctx.Services.AddSingleton(playlistClientMock.Object);
        ctx.Services.AddSingleton<ILogger<PlaylistEntriesList>>(NullLogger<PlaylistEntriesList>.Instance);
        ctx.Services.AddSingleton<ILogger<MediaSearchSelector>>(NullLogger<MediaSearchSelector>.Instance);

        var cut = ctx.RenderComponent<PlaylistEntriesList>(parameters => parameters
            .Add(p => p.PlaylistId, 1)
            .Add(p => p.IsManualMode, false));

        var searchSelector = cut.FindComponent<MediaSearchSelector>();
        await cut.InvokeAsync(() => searchSelector.Instance.OnMediaSelected.InvokeAsync(("Movie", 42L)));

        playlistClientMock.Verify(
            c => c.AddMediaToPlaylistAsync(1, It.Is<DtoAddMediaToPlaylistRequest>(r => r.MediaType == "Movie" && r.MediaId == 42)),
            Times.Once);
    }

    /// <summary>
    /// Minimal <see cref="VideoWebPlayerClient"/> stand-in for dependencies that
    /// <see cref="PlaylistEntriesList"/> and <see cref="MediaSearchSelector"/> inject but this test does
    /// not exercise (image URLs, live search); overrides the protected HTTP helper so no real HTTP call
    /// is ever attempted.
    /// </summary>
    private sealed class NoOpVideoWebPlayerClient : VideoWebPlayerClient
    {
        public NoOpVideoWebPlayerClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
        {
        }

        protected override Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
            => Task.FromResult((T)(object)new List<MediaEntryDto>());
    }
}
