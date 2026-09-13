using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Controllers.Models;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Minimal <see cref="VideoWebPlayerClient"/> stand-in for bUnit component tests that inject it but do
/// not exercise its HTTP-backed members (image URLs, live search): overrides the protected HTTP helper
/// so no real HTTP call is ever attempted. Shared by <c>PlaylistEntriesListTests</c> and
/// <c>PlaylistDetailTests</c>, which previously each declared an identical private copy of this class.
/// </summary>
public sealed class NoOpVideoWebPlayerClient : VideoWebPlayerClient
{
    public NoOpVideoWebPlayerClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
    {
    }

    protected override Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
        => Task.FromResult((T)(object)new List<MediaEntryDto>());
}
