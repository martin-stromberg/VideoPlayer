using Makaretu.Dns;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Announces the server via mDNS/DNS-SD in the local network so clients can discover it.
/// Effective only when the operator switch <c>Mdns:Enabled</c> and the admin switch
/// <c>Setup.MdnsAdvertisementEnabled</c> are both active (conjunction). The admin switch is
/// re-evaluated periodically so toggling it does not require a restart. Deregisters with
/// goodbye packets on shutdown. Failures are logged as warnings; the application keeps running.
/// </summary>
public sealed class MdnsAdvertiserWorker : BackgroundService
{
    private static readonly TimeSpan ReEvaluationInterval = TimeSpan.FromSeconds(60);

    private readonly IOptions<MdnsOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly IServer _server;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MdnsAdvertiserWorker> _logger;

    private ServiceDiscovery? _serviceDiscovery;
    private ServiceProfile? _profile;
    private bool _advertised;

    // Fail-closed: Advertise erst nach einer erfolgreichen DB-Lesung des Admin-Schalters.
    private bool _adminSwitchEnabled;

    /// <summary>
    /// Initializes a new instance of the <see cref="MdnsAdvertiserWorker"/> class.
    /// </summary>
    /// <param name="options">The bound <c>Mdns</c> options.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="server">The running server (for the actually bound addresses).</param>
    /// <param name="lifetime">The host application lifetime.</param>
    /// <param name="scopeFactory">Scope factory for reading the admin switch from the database.</param>
    /// <param name="logger">Logger instance.</param>
    public MdnsAdvertiserWorker(
        IOptions<MdnsOptions> options,
        IConfiguration configuration,
        IServer server,
        IHostApplicationLifetime lifetime,
        IServiceScopeFactory scopeFactory,
        ILogger<MdnsAdvertiserWorker> logger)
    {
        _options = options;
        _configuration = configuration;
        _server = server;
        _lifetime = lifetime;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Returns the effective advertisement state: active only when both the operator
    /// configuration switch and the admin switch are enabled (conjunction).
    /// </summary>
    /// <param name="configurationEnabled">The <c>Mdns:Enabled</c> operator switch.</param>
    /// <param name="adminSwitchEnabled">The <c>Setup.MdnsAdvertisementEnabled</c> admin switch.</param>
    /// <returns>Whether the mDNS advertisement is effectively enabled.</returns>
    internal static bool IsAdvertisementEnabled(bool configurationEnabled, bool adminSwitchEnabled)
        => configurationEnabled && adminSwitchEnabled;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await WaitForApplicationStartedAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // IOptions<MdnsOptions> ist statisch: bei deaktiviertem Operatorschalter kann die
        // Konjunktion nie wahr werden — die 60-s-Neuprüfung des Admin-Schalters entfällt.
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation(
                "[MdnsAdvertiserWorker] mDNS-Advertisement deaktiviert (Mdns:Enabled=false).");
            return;
        }

        await RefreshAdminSwitchAsync(stoppingToken);
        if (IsAdvertisementEnabled(_options.Value.Enabled, _adminSwitchEnabled))
        {
            TryAdvertise();
        }
        else
        {
            _logger.LogInformation(
                "[MdnsAdvertiserWorker] mDNS-Advertisement deaktiviert (Mdns:Enabled={ConfiguredEnabled}, Admin-Schalter={AdminEnabled}).",
                _options.Value.Enabled, _adminSwitchEnabled);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ReEvaluationInterval, stoppingToken);

                await RefreshAdminSwitchAsync(stoppingToken);
                var enabled = IsAdvertisementEnabled(_options.Value.Enabled, _adminSwitchEnabled);

                if (enabled && !_advertised)
                    TryAdvertise();
                else if (!enabled && _advertised)
                    TryUnadvertise();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MdnsAdvertiserWorker] Fehler bei der zyklischen Prüfung des Admin-Schalters.");
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        try
        {
            if (_advertised)
                TryUnadvertise();
            _serviceDiscovery?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MdnsAdvertiserWorker] Fehler beim Deregistrieren des mDNS-Advertisement.");
        }
    }

    private async Task WaitForApplicationStartedAsync(CancellationToken stoppingToken)
    {
        if (_lifetime.ApplicationStarted.IsCancellationRequested)
            return;

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = _lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);
    }

    private async Task RefreshAdminSwitchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settings = scope.ServiceProvider.GetRequiredService<ProgramSettingsService>();
            _adminSwitchEnabled = await settings.GetMdnsAdvertisementEnabledAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[MdnsAdvertiserWorker] Admin-Schalter konnte nicht gelesen werden; bisheriger Zustand bleibt bestehen.");
        }
    }

    private void TryAdvertise()
    {
        try
        {
            var boundAddresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
            var advertisement = MdnsServiceProfileBuilder.Build(_options.Value, _configuration, boundAddresses);

            _serviceDiscovery ??= new ServiceDiscovery();
            _profile = new ServiceProfile(
                new DomainName(advertisement.InstanceName),
                new DomainName(ToServiceName(advertisement.ServiceType)),
                (ushort)advertisement.Port)
            {
                HostName = new DomainName($"{advertisement.HostName}.local")
            };
            foreach (var txtRecord in advertisement.TxtRecords)
                _profile.AddProperty(txtRecord.Key, txtRecord.Value);

            _serviceDiscovery.Advertise(_profile);
            _serviceDiscovery.Announce(_profile);
            _advertised = true;

            _logger.LogInformation(
                "[MdnsAdvertiserWorker] Server per mDNS angekündigt: Instanz '{InstanceName}', Diensttyp '{ServiceType}', Port {Port}.",
                advertisement.InstanceName, advertisement.ServiceType, advertisement.Port);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MdnsAdvertiserWorker] mDNS-Advertisement konnte nicht gestartet werden.");
        }
    }

    private void TryUnadvertise()
    {
        try
        {
            if (_serviceDiscovery is not null && _profile is not null)
            {
                _serviceDiscovery.Unadvertise(_profile);
                _logger.LogInformation("[MdnsAdvertiserWorker] mDNS-Advertisement beendet (Goodbye-Pakete gesendet).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[MdnsAdvertiserWorker] Fehler beim Beenden des mDNS-Advertisement.");
        }
        finally
        {
            _profile = null;
            _advertised = false;
        }
    }

    private static string ToServiceName(string serviceType)
    {
        // The package joins the "local" domain itself; a configured ".local" suffix must be stripped.
        var name = serviceType.Trim().TrimEnd('.');
        if (name.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            name = name[..^".local".Length];
        return name;
    }
}
