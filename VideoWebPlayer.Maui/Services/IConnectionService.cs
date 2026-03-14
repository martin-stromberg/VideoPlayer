namespace VideoWebPlayer.Maui.Services;

public interface IConnectionService
{
    /// <summary>
    /// Try to connect to the server at the given base address. Returns true when server responds.
    /// </summary>
    Task<bool> TryConnectAsync(string baseAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the connection workflow and update the provided HomePageViewModel with visibility/connecting flags.
    /// Returns a ConnectionState describing the next action.
    /// </summary>
    Task<ConnectionState> StartConnectionWorkflowAsync(ViewModels.HomePageViewModel viewModel, IServiceProvider services, CancellationToken cancellationToken = default);
}

public enum ConnectionState
{
    Connected,
    NeedsServerSetup,
    NeedsLogin,
    Offline
}
