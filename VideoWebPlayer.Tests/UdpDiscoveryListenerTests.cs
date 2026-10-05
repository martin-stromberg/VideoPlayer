using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        var messages = new ConcurrentQueue<string>();
        var fail = true;
        var listener = new UdpDiscoveryListener(port, _ =>
            fail
                ? Task.FromException<string>(new InvalidOperationException("Auflösung fehlgeschlagen"))
                : Task.FromResult("http://192.168.1.5:5000"),
            new ListLogger<UdpDiscoveryListener>(messages));
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
                    logged = await WaitForLogAsync(messages, TimeSpan.FromMilliseconds(200), ct);
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

    private static async Task<bool> WaitForLogAsync(
        ConcurrentQueue<string> messages, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (messages.Any(m => m.Contains("[UdpDiscoveryListener]")))
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
