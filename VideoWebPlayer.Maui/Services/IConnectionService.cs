namespace VideoWebPlayer.Maui.Services;

public interface IConnectionService
{
    /// <summary>
    /// Try to connect to the server at the given base address. Returns true when server responds.
    /// </summary>
    Task<bool> TryConnectAsync(string baseAddress, CancellationToken cancellationToken = default);
}
