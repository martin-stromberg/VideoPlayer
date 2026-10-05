using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace VideoWebPlayer.Services;

/// <summary>
/// UDP-Listener für Discovery-Anfragen im lokalen Netzwerk.
/// Antwortet auf Broadcasts mit der Serveradresse.
/// </summary>
public class UdpDiscoveryListener
{
    private readonly int _port;
    private readonly Func<CancellationToken, Task<string>> _responseFactory;
    private readonly ILogger<UdpDiscoveryListener> _logger;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Erstellt einen neuen UDP-Discovery-Listener.
    /// </summary>
    /// <param name="port">UDP-Port für Discovery-Anfragen.</param>
    /// <param name="responseFactory">Liefert pro Anfrage die zu meldende Basis-URL.</param>
    /// <param name="logger">Logger instance.</param>
    public UdpDiscoveryListener(
        int port,
        Func<CancellationToken, Task<string>> responseFactory,
        ILogger<UdpDiscoveryListener> logger)
    {
        _port = port;
        _responseFactory = responseFactory;
        _logger = logger;
    }

    /// <summary>
    /// Startet den UDP-Listener im Hintergrund.
    /// </summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenAsync(_cts.Token));
    }

    /// <summary>
    /// Stoppt den UDP-Listener und beendet die Hintergrundaufgabe.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        using var udp = new UdpClient(_port);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync();
                var request = System.Text.Encoding.UTF8.GetString(result.Buffer);
                if (request == "VIDEOWEBPLAYER_DISCOVERY")
                {
                    var serverAddress = await _responseFactory(cancellationToken);
                    var response = System.Text.Encoding.UTF8.GetBytes($"VIDEOWEBPLAYER_SERVER:{serverAddress}");
                    await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException)
            {
                // Transient: unter Windows meldet UDP ein ICMP "Port unreachable" auf eine
                // frühere Antwort als SocketException (ConnectionReset) — kein Fehler des
                // Listeners, die Schleife läuft weiter.
            }
            catch (Exception ex)
            {
                // Fail-open: ein Fehler in der URL-Auflösung oder beim Senden darf den
                // Listener nicht kippen, bleibt aber diagnostizierbar.
                _logger.LogWarning(
                    ex,
                    "[UdpDiscoveryListener] Discovery-Anfrage auf Port {Port} konnte nicht beantwortet werden.",
                    _port);
            }
        }
    }
}
