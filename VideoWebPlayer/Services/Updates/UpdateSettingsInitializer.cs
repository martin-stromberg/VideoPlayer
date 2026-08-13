namespace VideoWebPlayer.Services.Updates;

/// <summary>
/// Applies persisted update settings before updater hosted services start their periodic work.
/// </summary>
public sealed class UpdateSettingsInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<UpdateSettingsInitializer> _logger;

    /// <summary>
    /// Creates a new initializer.
    /// </summary>
    public UpdateSettingsInitializer(IServiceProvider services, ILogger<UpdateSettingsInitializer> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<IUpdateSettingsService>();
            await settings.ApplyToRuntimeOptionsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Persisted update settings could not be applied at startup.");
            throw;
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
