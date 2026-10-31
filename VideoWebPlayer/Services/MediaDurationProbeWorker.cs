using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services.Backups;

namespace VideoWebPlayer.Services;

/// <summary>
/// Background worker that probes the playback duration of media items that have not been probed
/// yet — both newly scanned files and the pre-existing library (backfill), without user interaction.
/// </summary>
public sealed class MediaDurationProbeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MediaProbeOptions> _options;
    private readonly ILogger<MediaDurationProbeWorker> _logger;
    private readonly IProgramScheduleSignal? _signal;
    private readonly TimeSpan _initialDelay;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaDurationProbeWorker"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory used to resolve the scoped probe service.</param>
    /// <param name="options">Media probe options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="initialDelay">Optional delay before the first probe pass (default 10 seconds).</param>
    /// <param name="timeProvider">Optional time provider, primarily for testing.</param>
    /// <param name="signal">Optional wake-up line whose scan scope bundles the per-batch probe
    /// signals into a single wake per probe pass; when <see langword="null"/> the signal is
    /// resolved from the DI scope.</param>
    public MediaDurationProbeWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MediaProbeOptions> options,
        ILogger<MediaDurationProbeWorker> logger,
        TimeSpan? initialDelay = null,
        TimeProvider? timeProvider = null,
        IProgramScheduleSignal? signal = null)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(10);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _signal = signal;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[MediaDurationProbeWorker] Started");

        try
        {
            if (_initialDelay > TimeSpan.Zero)
                await Task.Delay(_initialDelay, _timeProvider, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();

                    // Feature-Flag aus: Laufzeitermittlung pausiert (sie dient nur dem Sender).
                    if (await settings.GetTvSenderEnabledAsync(stoppingToken))
                    {
                        var service = scope.ServiceProvider.GetRequiredService<MediaDurationProbeService>();
                        var gate = scope.ServiceProvider.GetService<IBackgroundProcessingGate>();

                        await using var lease = gate is null
                            ? null
                            : await gate.EnterOperationAsync("Laufzeitermittlung", stoppingToken);

                        var batchSize = _options.Value.EffectiveBatchSize;
                        // The backlog loop fires NotifyScheduleChanged once per batch — the scan
                        // scope suppresses the per-batch wake-ups and the schedule worker wakes
                        // exactly once when the whole pass is done.
                        var signal = _signal ?? scope.ServiceProvider.GetService<IProgramScheduleSignal>();
                        using var scan = signal?.BeginScan();
                        int processed;
                        do
                        {
                            processed = await service.ProbePendingAsync(batchSize, stoppingToken);
                        }
                        while (processed > 0 && !stoppingToken.IsCancellationRequested);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[MediaDurationProbeWorker] Fehler bei der Laufzeitermittlung.");
                }

                await Task.Delay(TimeSpan.FromSeconds(_options.Value.EffectiveIntervalSeconds), _timeProvider, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("[MediaDurationProbeWorker] Stopped");
    }
}
