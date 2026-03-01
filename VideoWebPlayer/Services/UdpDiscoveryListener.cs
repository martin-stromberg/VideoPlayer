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
    private readonly string _serverAddress;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Erstellt einen neuen UDP-Discovery-Listener.
    /// </summary>
    /// <param name="port">UDP-Port für Discovery-Anfragen.</param>
    /// <param name="serverAddress">Die Adresse, die als Antwort gesendet wird.</param>
    public UdpDiscoveryListener(int port, string serverAddress)
    {
        _port = port;
        _serverAddress = serverAddress;
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
                    var response = System.Text.Encoding.UTF8.GetBytes($"VIDEOWEBPLAYER_SERVER:{_serverAddress}");
                    await udp.SendAsync(response, response.Length, result.RemoteEndPoint);
                }
            }
            catch { }
        }
    }
}
