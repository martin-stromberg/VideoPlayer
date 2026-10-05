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
    private Session? _session;

    /// <summary>
    /// Koppelt die CancellationTokenSource einer gestarteten Listener-Runde mit
    /// ihrem Hintergrund-Task, damit <see cref="Stop"/> genau den Task abwarten
    /// kann, der zu der abgebrochenen Source gehört.
    /// </summary>
    private sealed class Session
    {
        public CancellationTokenSource Cts { get; } = new();

        // Wird von Start() unmittelbar nach dem gewonnenen CAS gesetzt; Stop()
        // kann ihn in diesem schmalen Fenster noch nicht sehen und wartet kurz.
        public Task? ListenerTask;
    }

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
    /// Startet den UDP-Listener im Hintergrund. Ein weiterer Aufruf, während der
    /// Listener läuft (oder ein nebenläufiger Aufruf), ist ein No-Op — es läuft
    /// immer höchstens ein Hintergrund-Task, der sich mit <see cref="Stop"/>
    /// beenden lässt.
    /// </summary>
    public void Start()
    {
        var session = new Session();
        if (Interlocked.CompareExchange(ref _session, session, null) is not null)
        {
            // Bereits gestartet (oder ein paralleler Start hat den CAS gewonnen):
            // die neue Source freigeben, statt einen verwaisten zweiten Listener
            // zu starten, den Stop() nie erreichen könnte.
            session.Cts.Dispose();
            return;
        }

        // Den Token synchron lesen und ins Lambda übergeben: läse das Lambda
        // ihn erst auf dem Pool-Thread, könnte ein dazwischenliegendes Stop()
        // die Source bereits freigegeben haben — der Token-Getter würfe dann
        // eine ObjectDisposedException und ließe die Task unbeobachtet faulten.
        var token = session.Cts.Token;
        Volatile.Write(ref session.ListenerTask, Task.Run(() => ListenAsync(token)));
    }

    /// <summary>
    /// Stoppt den UDP-Listener, wartet das Ende der Hintergrundaufgabe ab und
    /// gibt den Socket frei. Danach kann der Listener erneut gestartet werden.
    /// </summary>
    public void Stop()
    {
        var session = Interlocked.Exchange(ref _session, null);
        if (session is null)
            return;

        // Cancel() weckt den blockierenden ReceiveAsync über den CancellationToken auf.
        session.Cts.Cancel();

        // Start() hinterlegt den Task unmittelbar nach dem gewonnenen CAS — ein
        // sehr frühes Stop() kann ihn in diesem Fenster noch nicht sehen.
        var task = Volatile.Read(ref session.ListenerTask);
        var pollDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (task is null && DateTime.UtcNow < pollDeadline)
        {
            Thread.Sleep(10);
            task = Volatile.Read(ref session.ListenerTask);
        }

        try
        {
            // Auf das Task-Ende warten: erst dann ist der Socket wirklich
            // freigegeben und ein direkt folgendes Start() kann den Port
            // erneut binden. Begrenzt, damit Stop() nicht hängen kann.
            task?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Fehler des Listener-Tasks werden bereits in ListenAsync protokolliert.
        }

        session.Cts.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        // Stop() kann bereits gelaufen sein, bevor der Pool-Thread die Task
        // beginnt — dann den Socket gar nicht erst binden.
        if (cancellationToken.IsCancellationRequested)
            return;

        UdpClient udp;
        try
        {
            udp = new UdpClient(_port);
        }
        catch (Exception ex)
        {
            // Kann der Socket nicht gebunden werden (z. B. Port bereits belegt), hat die
            // Hintergrund-Task keine Funktion — einmalig als Fehler protokollieren statt
            // die Task unbeobachtet faulten zu lassen.
            _logger.LogError(
                ex,
                "[UdpDiscoveryListener] UDP-Listener auf Port {Port} konnte nicht gestartet werden.",
                _port);
            return;
        }

        using (udp)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(cancellationToken);
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
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    // Stop() kann die CancellationTokenSource freigeben, während ReceiveAsync
                    // den Token noch registriert — angefordertes Beenden, kein Fehler.
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    // Der per Cancel() abgebrochene Empfang kann auch als SocketException
                    // (OperationAborted) ankommen — angefordertes Beenden, kein Fehler.
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
}
