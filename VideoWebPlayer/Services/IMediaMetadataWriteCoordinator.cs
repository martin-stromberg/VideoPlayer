namespace VideoWebPlayer.Services;

/// <summary>
/// Coordinates scanner and manual editor writes that affect media metadata.
/// </summary>
public interface IMediaMetadataWriteCoordinator
{
    /// <summary>
    /// Enters an exclusive metadata write section.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A lease that releases the section when disposed.</returns>
    Task<IAsyncDisposable> EnterAsync(CancellationToken cancellationToken);
}
