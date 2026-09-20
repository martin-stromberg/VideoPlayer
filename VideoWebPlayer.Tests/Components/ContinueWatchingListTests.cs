using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Shared.Home;
using VideoWebPlayer.Components.Shared.Media;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Regression tests for the "duplicate dictionary key / Blazor @key collision" bug (Weiterschauen mit
/// Playlist-Bezug, Schritt 6 Nachbesserung, Problem 1/2): before the fix, <see cref="ContinueWatchingList"/>
/// indexed its display-data dictionaries and its <c>@key</c> attribute by <c>ContinueWatchingDto.Entry.Id</c>
/// (the media id), which collides whenever the same media appears multiple times in the list with
/// different <see cref="ContinueWatchingDto.PlaylistId"/> values - overwriting each other's display data
/// and crashing the Blazor circuit with a duplicate-key error on render. The fix indexes by the new,
/// per-row-unique <see cref="ContinueWatchingDto.Id"/> instead.
/// </summary>
public class ContinueWatchingListTests
{
    [Fact]
    public void ContinueWatchingList_MultipleVariantsOfSameMedia_RendersWithUniqueKeys()
    {
        var client = new FakeContinueWatchingVideoWebPlayerClient
        {
            Items = BuildThreeVariantsOfSameMovie()
        };

        using var ctx = CreateTestContext(client);

        // Rendering itself must not throw: before the fix, three MediaBox siblings sharing the same
        // @key="it.Entry.Id" (all 42) crash the renderer with "More than one sibling ... has the same key
        // value" as soon as more than one variant of the same media is present.
        var cut = ctx.Render<ContinueWatchingList>();

        var shells = cut.FindAll(".media-box-shell");
        Assert.Equal(3, shells.Count);

        // Every variant must keep its own, correct subtitle - before the fix, all three collided on
        // dictionary key 42 and ended up showing the same (last-written) subtitle/link/image.
        var subtitles = cut.FindAll(".media-subtitle-text").Select(e => e.TextContent).ToList();
        Assert.Equal(2, subtitles.Count);
        Assert.Contains("In Playlist: Playlist A", subtitles);
        Assert.Contains("In Playlist: Playlist B", subtitles);

        var links = cut.FindAll(".media-box-link").Select(e => e.GetAttribute("href")).ToList();
        Assert.Contains("/playlists/1?entryId=201", links);
        Assert.Contains("/playlists/2?entryId=202", links);
        Assert.Contains(links, l => l is not null && l.StartsWith("/moviecollection/55"));
    }

    [Fact]
    public async Task ContinueWatchingList_ContextActionOnMultipleVariants_TargetsCorrectEntryById()
    {
        var client = new FakeContinueWatchingVideoWebPlayerClient
        {
            Items = BuildThreeVariantsOfSameMovie()
        };

        using var ctx = CreateTestContext(client);
        var cut = ctx.Render<ContinueWatchingList>();

        var mediaBoxes = cut.FindComponents<MediaBox>();
        Assert.Equal(3, mediaBoxes.Count);

        // "Ausblenden" fuer den mittleren Eintrag (PlaylistId = 2) auswaehlen.
        await cut.InvokeAsync(() => mediaBoxes[1].Instance.OnActionSelected.InvokeAsync("hide"));

        var call = Assert.Single(client.Calls);
        Assert.Equal("hide", call.Action);
        Assert.Equal("movie", call.MediaType);
        Assert.Equal(42, call.MediaId);
        Assert.Equal(2, call.PlaylistId);

        // Die Liste bleibt konsistent: die anderen beiden Varianten sind weiterhin vorhanden und zeigen
        // weiterhin ihre jeweils korrekten Daten.
        var remainingShells = cut.FindAll(".media-box-shell");
        Assert.Equal(2, remainingShells.Count);

        var remainingLinks = cut.FindAll(".media-box-link").Select(e => e.GetAttribute("href")).ToList();
        Assert.Contains("/playlists/1?entryId=201", remainingLinks);
        Assert.Contains(remainingLinks, l => l is not null && l.StartsWith("/moviecollection/55"));
        Assert.DoesNotContain("/playlists/2?entryId=202", remainingLinks);
    }

    /// <summary>
    /// Builds three <see cref="ContinueWatchingDto"/> variants of the same movie (media id 42): one bound
    /// to playlist 1, one bound to playlist 2, and one without a playlist reference - the exact scenario
    /// that used to collide before <see cref="ContinueWatchingDto.Id"/> was introduced.
    /// </summary>
    /// <returns>The three built variants.</returns>
    private static List<ContinueWatchingDto> BuildThreeVariantsOfSameMovie() =>
    [
        BuildMovieVariant(id: 101, playlistId: 1, playlistName: "Playlist A", playlistEntryId: 201, positionSeconds: 100),
        BuildMovieVariant(id: 102, playlistId: 2, playlistName: "Playlist B", playlistEntryId: 202, positionSeconds: 200),
        BuildMovieVariant(id: 103, playlistId: null, playlistName: null, playlistEntryId: null, positionSeconds: 300, includeCollection: true)
    ];

    private static ContinueWatchingDto BuildMovieVariant(long id, long? playlistId, string? playlistName, long? playlistEntryId, long positionSeconds, bool includeCollection = false)
    {
        var movie = new DtoMovie { Id = 42, Name = "Test Movie" };
        if (includeCollection)
            movie.Collection = new DtoMovieCollection { Id = 55, Name = "Test Collection" };

        return new ContinueWatchingDto
        {
            Id = id,
            MediaType = "movie",
            Entry = movie,
            Title = "Test Movie",
            PositionSeconds = positionSeconds,
            PlaylistId = playlistId,
            PlaylistName = playlistName,
            PlaylistEntryId = playlistEntryId
        };
    }

    private static global::Bunit.BunitContext CreateTestContext(VideoWebPlayerClient client)
    {
        var ctx = new global::Bunit.BunitContext();
        ctx.AddAuthorization().SetAuthorized("test-user");
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddSingleton(client);
        ctx.Services.AddSingleton(new EventManager());
        return ctx;
    }

    /// <summary>
    /// Minimal <see cref="VideoWebPlayerClient"/> stand-in that serves a fixed continue-watching list and
    /// records every hide/skip call (media type, media id and playlist id) instead of performing real HTTP
    /// requests, by overriding the protected HTTP helpers <see cref="VideoWebPlayerClient"/>'s public
    /// continue-watching methods are built on (those public methods are not themselves virtual).
    /// </summary>
    private sealed class FakeContinueWatchingVideoWebPlayerClient : VideoWebPlayerClient
    {
        public List<ContinueWatchingDto> Items { get; set; } = new();

        public List<(string MediaType, long MediaId, long? PlaylistId, string Action)> Calls { get; } = new();

        public FakeContinueWatchingVideoWebPlayerClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
        {
        }

        protected override Task<T> HttpGetAsync<T>(string endPoint)
        {
            if (endPoint == "api/continue-watching")
                return Task.FromResult((T)(object)Items.ToArray());

            throw new InvalidOperationException($"Unerwarteter GET-Aufruf: {endPoint}");
        }

        protected override Task<T> HttpPostAsync<T>(string endPoint, HttpContent args, bool skipReauthorize = false)
        {
            var json = args.ReadAsStringAsync().GetAwaiter().GetResult();
            var payload = JsonSerializer.Deserialize<JsonElement>(json);
            var mediaType = payload.GetProperty("MediaType").GetString()!;
            var mediaId = payload.GetProperty("MediaId").GetInt64();
            var playlistIdProperty = payload.GetProperty("PlaylistId");
            long? playlistId = playlistIdProperty.ValueKind == JsonValueKind.Null ? null : playlistIdProperty.GetInt64();

            if (endPoint == "api/continue-watching/hide")
            {
                Calls.Add((mediaType, mediaId, playlistId, "hide"));
                Items.RemoveAll(i => i.Entry.Id == mediaId && i.PlaylistId == playlistId);
                return Task.FromResult((T)(object)new ContinueWatchingMutationResult("removed", "Ausgeblendet."));
            }

            if (endPoint == "api/continue-watching/skip")
            {
                Calls.Add((mediaType, mediaId, playlistId, "skip"));
                return Task.FromResult((T)(object)new ContinueWatchingMutationResult("skipped", "Übersprungen."));
            }

            throw new InvalidOperationException($"Unerwarteter POST-Aufruf: {endPoint}");
        }
    }
}
