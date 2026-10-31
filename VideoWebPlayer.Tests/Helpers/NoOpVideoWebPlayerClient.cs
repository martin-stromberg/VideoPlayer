using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Client;
using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Controllers.Models;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Minimal <see cref="VideoWebPlayerClient"/> stand-in for bUnit component tests that inject it but do
/// not exercise its HTTP-backed members (image URLs, live search, genre options): overrides the protected
/// HTTP helper so no real HTTP call is ever attempted. Shared by the bUnit component tests, which
/// previously each declared an identical private copy of this class.
/// </summary>
public sealed class NoOpVideoWebPlayerClient : VideoWebPlayerClient
{
    private readonly DtoMediaSource[] _sources;

    /// <param name="sources">
    /// Quellen, die <c>HttpGetAsync&lt;DtoMediaSource[]&gt;</c> (z. B. <c>RequestSourcesAsync</c>)
    /// liefern soll — für bUnit-Tests des <c>NavMenu</c>, das Quellen rendert. Default: leeres Array.
    /// </param>
    public NoOpVideoWebPlayerClient(DtoMediaSource[]? sources = null)
        : base(new HttpClient(), NullLogger<VideoWebPlayerClient>.Instance)
    {
        _sources = sources ?? Array.Empty<DtoMediaSource>();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The one-argument overload is a separate virtual method in the base class — without overriding
    /// it, calls like <c>RequestSourcesAsync</c> (which uses this overload) would attempt a real HTTP
    /// request. Routing it to the two-argument overload keeps a single fake implementation.
    /// </remarks>
    protected override Task<T> HttpGetAsync<T>(string endPoint)
        => HttpGetAsync<T>(endPoint, CancellationToken.None);

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
            : typeof(T) == typeof(DtoMediaSource[])
                ? _sources
                : typeof(T) == typeof(DtoProgramGuideNow)
                    ? new DtoProgramGuideNow()
                    : typeof(T) == typeof(DtoProgramGuideEntry[])
                        ? Array.Empty<DtoProgramGuideEntry>()
                        : (object)new List<MediaEntryDto>();
        return Task.FromResult((T)result);
    }
}
