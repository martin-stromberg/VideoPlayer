using Microsoft.Extensions.Options;

namespace msTools.Backup;

/// <summary>
/// Provides backup options from the standard options system.
/// </summary>
public sealed class OptionsBackupOptionsProvider : IBackupOptionsProvider
{
    private readonly IOptionsMonitor<BackupOptions> _options;

    /// <summary>
    /// Creates a new provider.
    /// </summary>
    public OptionsBackupOptionsProvider(IOptionsMonitor<BackupOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
        => Task.FromResult(_options.CurrentValue);
}
