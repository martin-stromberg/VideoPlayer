using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class UdpDiscoveryListenerTests
{
    [Fact]
    public async Task Start_AnswersDiscoveryRequest_WithResolvedAddress()
    {
        var ct = TestContext.Current.CancellationToken;
        var port = GetFreeUdpPort();
        var listener = new UdpDiscoveryListener(
            port,
            _ => Task.FromResult("http://192.168.1.5:5000/"),
            NullLogger<UdpDiscoveryListener>.Instance);
        listener.Start();
        try
        {
            var response = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);

            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5000/", response);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Start_ResolvesAddress_PerRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        var port = GetFreeUdpPort();
        var call = 0;
        var listener = new UdpDiscoveryListener(port, _ =>
        {
            call++;
            return Task.FromResult($"http://192.168.1.5:50{call}0");
        }, NullLogger<UdpDiscoveryListener>.Instance);
        listener.Start();
        try
        {
            var first = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);
            var second = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);

            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5010", first);
            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5020", second);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Start_LogsWarning_AndKeepsListening_WhenFactoryThrows()
    {
        // Fail-open: eine fehlschlagende URL-Auflösung wird als Warnung protokolliert
        // und darf den Listener nicht kippen — die nächste Anfrage wird beantwortet.
        var ct = TestContext.Current.CancellationToken;
        var port = GetFreeUdpPort();
        var logger = new ListLogger<UdpDiscoveryListener>(new ConcurrentQueue<string>());
        var fail = true;
        var listener = new UdpDiscoveryListener(port, _ =>
            fail
                ? Task.FromException<string>(new InvalidOperationException("Auflösung fehlgeschlagen"))
                : Task.FromResult("http://192.168.1.5:5000"),
            logger);
        listener.Start();
        try
        {
            // Factory wirft → keine Antwort, aber Warnung im Log. Wie in
            // SendDiscoveryUntilResponseAsync wird wiederholt gesendet, weil Datagramme
            // vor dem Binden des Listener-Sockets verloren gehen können.
            using (var client = new UdpClient())
            {
                var request = Encoding.UTF8.GetBytes("VIDEOWEBPLAYER_DISCOVERY");
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
                var logged = false;
                while (DateTime.UtcNow < deadline && !logged)
                {
                    ct.ThrowIfCancellationRequested();
                    await client.SendAsync(request, request.Length, new IPEndPoint(IPAddress.Loopback, port));
                    logged = await WaitForLogAsync(logger, LogLevel.Warning, TimeSpan.FromMilliseconds(200), ct);
                }

                Assert.True(logged, "Keine Warnung für die fehlgeschlagene URL-Auflösung protokolliert.");
            }

            // Factory liefert wieder → Listener lebt noch und antwortet.
            fail = false;
            var response = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);

            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5000", response);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Stop_ReleasesSocket_WithoutIncomingDatagram()
    {
        // Stop() muss den blockierenden ReceiveAsync per CancellationToken aufwecken
        // und den Socket freigeben — ohne dass noch ein Datagramm eintreffen muss.
        var ct = TestContext.Current.CancellationToken;
        var port = GetFreeUdpPort();
        var listener = new UdpDiscoveryListener(
            port,
            _ => Task.FromResult("http://192.168.1.5:5000/"),
            NullLogger<UdpDiscoveryListener>.Instance);
        listener.Start();
        try
        {
            // Erst sicherstellen, dass der Listener gebunden hat und antwortet —
            // sonst wäre der Port ohnehin frei.
            await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);
        }
        finally
        {
            listener.Stop();
        }

        // Ein zweites Bind auf denselben Port (ohne ReuseAddress) darf erst gelingen,
        // wenn der Listener-Socket wirklich geschlossen ist.
        var released = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var probe = new UdpClient(port);
                released = true;
                break;
            }
            catch (SocketException)
            {
                await Task.Delay(50, ct);
            }
        }

        Assert.True(released, "Der UDP-Port wurde nach Stop() nicht freigegeben.");
    }

    [Fact]
    public async Task Start_LogsError_WhenPortAlreadyBound()
    {
        // Schlägt das Binden fehl (Port belegt), muss der Fehler einmalig protokolliert
        // werden statt die Hintergrund-Task unbeobachtet faulten zu lassen.
        var ct = TestContext.Current.CancellationToken;
        var port = GetFreeUdpPort();
        var logger = new ListLogger<UdpDiscoveryListener>(new ConcurrentQueue<string>());

        using var blocker = new UdpClient(port);
        var listener = new UdpDiscoveryListener(
            port,
            _ => Task.FromResult("http://192.168.1.5:5000/"),
            logger);
        listener.Start();
        try
        {
            var logged = await WaitForLogAsync(
                logger, LogLevel.Error, TimeSpan.FromSeconds(10), ct, "konnte nicht gestartet werden");

            Assert.True(logged, "Der Bind-Fehler wurde nicht als Fehler protokolliert.");
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Start_AfterStop_RebindsPort_AndAnswers()
    {
        // Neustart-Vertrag: Stop() wartet das Ende des Hintergrund-Tasks ab —
        // danach ist der Socket frei und ein sofortiges Start() kann den Port
        // erneut binden, statt am noch gebundenen alten Socket zu scheitern.
        var ct = TestContext.Current.CancellationToken;
        var port = GetFreeUdpPort();
        var listener = new UdpDiscoveryListener(
            port,
            _ => Task.FromResult("http://192.168.1.5:5000/"),
            NullLogger<UdpDiscoveryListener>.Instance);
        listener.Start();
        try
        {
            var response = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);
            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5000/", response);

            listener.Stop();

            // Nach Stop() muss der Port unmittelbar frei sein — bliebe der alte
            // Socket noch gebunden, würfe das Probe-Bind eine SocketException.
            using (var probe = new UdpClient(port))
            {
            }

            listener.Start();

            response = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);
            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5000/", response);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Start_CalledTwice_KeepsSingleListener_AndStopReleasesPort()
    {
        // Start() ist idempotent: ein zweiter Aufruf darf keinen zweiten
        // Hintergrund-Task auf denselben Port loslassen, der nach Stop()
        // verwaist weiterläuft.
        var ct = TestContext.Current.CancellationToken;
        var port = GetFreeUdpPort();
        var listener = new UdpDiscoveryListener(
            port,
            _ => Task.FromResult("http://192.168.1.5:5000/"),
            NullLogger<UdpDiscoveryListener>.Instance);
        listener.Start();
        try
        {
            // Erst die Antwort des ersten Listeners abwarten, dann erneut starten:
            // so ist sichergestellt, dass der erste Task gebunden hat und sein
            // Token bereits gelesen wurde — der Doppelstart trifft einen laufenden
            // Listener, keine Start-Race.
            var response = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);
            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5000/", response);

            listener.Start();

            // Der laufende Listener bleibt der einzige und antwortet weiter.
            response = await SendDiscoveryUntilResponseAsync(port, TimeSpan.FromSeconds(10), ct);
            Assert.Equal("VIDEOWEBPLAYER_SERVER:http://192.168.1.5:5000/", response);
        }
        finally
        {
            listener.Stop();
        }

        // Genau ein Stop() muss den einzigen Listener freigeben — bliebe ein
        // verwaister erster Task gebunden, würde das Probe-Bind fehlschlagen.
        var released = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var probe = new UdpClient(port);
                released = true;
                break;
            }
            catch (SocketException)
            {
                await Task.Delay(50, ct);
            }
        }

        Assert.True(released, "Nach doppeltem Start() und einem Stop() blieb der UDP-Port belegt.");
    }

    private static async Task<bool> WaitForLogAsync(
        ListLogger<UdpDiscoveryListener> logger, LogLevel level, TimeSpan timeout,
        CancellationToken cancellationToken, string requiredSubstring = "[UdpDiscoveryListener]")
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (logger.Entries.Any(e => e.Level == level && e.Message.Contains(requiredSubstring)))
                return true;

            await Task.Delay(50, cancellationToken);
        }

        return false;
    }

    private static int GetFreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static async Task<string> SendDiscoveryUntilResponseAsync(
        int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var client = new UdpClient();
        var request = Encoding.UTF8.GetBytes("VIDEOWEBPLAYER_DISCOVERY");
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendAsync(request, request.Length, new IPEndPoint(IPAddress.Loopback, port));

            // Der Listener bindet seinen Socket im Hintergrund — Datagramme, die vor dem
            // Binden eintreffen, gehen verloren; deshalb wird mit kurzen Empfangsfenstern
            // wiederholt gesendet, statt die Bindung anderweitig abzuwarten. Unter Windows
            // meldet der Empfang eines solchen verlorenen Datagramms die ICMP-Antwort
            // "Port unreachable" als SocketException (ConnectionReset) — ebenfalls ignorieren.
            var remaining = deadline - DateTime.UtcNow;
            var wait = remaining < TimeSpan.FromMilliseconds(500) ? remaining : TimeSpan.FromMilliseconds(500);
            try
            {
                var result = await client.ReceiveAsync().WaitAsync(wait, cancellationToken);
                return Encoding.UTF8.GetString(result.Buffer);
            }
            catch (TimeoutException)
            {
            }
            catch (SocketException)
            {
            }
        }

        throw new TimeoutException("Keine Discovery-Antwort innerhalb des Zeitlimits erhalten.");
    }
}
