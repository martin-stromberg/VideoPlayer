using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers.Models;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Minimal <see cref="VideoWebPlayerClient"/> stand-in for bUnit component tests that inject it but do
/// not exercise its HTTP-backed members (image URLs, live search, genre options): overrides the protected
/// HTTP helper so no real HTTP call is ever attempted. Shared by <c>PlaylistEntriesListTests</c> and
/// <c>PlaylistDetailTests</c>, which previously each declared an identical private copy of this class.
/// </summary>
public sealed class NoOpVideoWebPlayerClient : VideoWebPlayerClient
{
    public NoOpVideoWebPlayerClient() : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns an empty result matching <typeparamref name="T"/> for every known result type any test
    /// double consumer might request, rather than always returning a <see cref="List{MediaEntryDto}"/>
    /// cast to <typeparamref name="T"/> (the pre-Entwicklungsschritt-9 behavior, which would throw an
    /// <see cref="InvalidCastException"/> for any other <typeparamref name="T"/>, e.g.
    /// <see cref="List{DtoGenreOption}"/> as requested by <c>PlaylistGenreEditor</c>).
    /// </remarks>
    protected override Task<T> HttpGetAsync<T>(string endPoint, CancellationToken cancellationToken)
    {
        object result = typeof(T) == typeof(List<DtoGenreOption>)
            ? new List<DtoGenreOption>()
            : new List<MediaEntryDto>();
        return Task.FromResult((T)result);
    }
}
