using Microsoft.AspNetCore.SignalR.Client;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// SignalR Service für Echtzeit-Updates von Media-Listen.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _isConnected;

    /// <summary>
    /// Event wird ausgelöst wenn Continue-Watching Liste aktualisiert wurde.
    /// </summary>
    public event EventHandler? ContinueWatchingUpdated;

    /// <summary>
    /// Event wird ausgelöst wenn Favoriten geändert wurden.
    /// </summary>
    public event EventHandler? FavoritesChanged;

    /// <summary>
    /// Event wird ausgelöst wenn neue Videos gescannt wurden.
    /// </summary>
    public event EventHandler<NewVideosScannedEventArgs>? NewVideosScanned;

    /// <summary>
    /// Verbindet mit dem SignalR Hub.
    /// </summary>
    public async Task ConnectAsync(string serverAddress, string token)
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_isConnected)
            {
                System.Diagnostics.Debug.WriteLine("[SignalR] Already connected");
                return;
            }

            if (!serverAddress.StartsWith("http"))
            {
                serverAddress = $"http://{serverAddress}";
            }
            serverAddress = serverAddress.TrimEnd('/');

            var hubUrl = $"{serverAddress}/hubs/mediaupdate";
            System.Diagnostics.Debug.WriteLine($"[SignalR] Connecting to: {hubUrl}");

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .WithAutomaticReconnect()
                .Build();

            // Event-Handler registrieren
            _connection.On("ContinueWatchingUpdated", () =>
            {
                System.Diagnostics.Debug.WriteLine("[SignalR] ContinueWatchingUpdated received");
                ContinueWatchingUpdated?.Invoke(this, EventArgs.Empty);
            });

            _connection.On("FavoritesChanged", () =>
            {
                System.Diagnostics.Debug.WriteLine("[SignalR] FavoritesChanged received");
                FavoritesChanged?.Invoke(this, EventArgs.Empty);
            });

            _connection.On<long, int>("NewVideosScanned", (sourceId, count) =>
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] NewVideosScanned received: Source {sourceId}, Count {count}");
                NewVideosScanned?.Invoke(this, new NewVideosScannedEventArgs(sourceId, count));
            });

            // Reconnection-Handler
            _connection.Reconnecting += (error) =>
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] Reconnecting... Error: {error?.Message}");
                return Task.CompletedTask;
            };

            _connection.Reconnected += (connectionId) =>
            {
                System.Diagnostics.Debug.WriteLine($"[SignalR] Reconnected. ConnectionId: {connectionId}");
                return Task.CompletedTask;
            };

            _connection.Closed += async (error) =>
            {
                _isConnected = false;
                System.Diagnostics.Debug.WriteLine($"[SignalR] Connection closed. Error: {error?.Message}");
                
                // Versuche automatisch zu reconnecten
                await Task.Delay(TimeSpan.FromSeconds(5));
                try
                {
                    if (_connection != null && _connection.State == HubConnectionState.Disconnected)
                    {
                        await _connection.StartAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SignalR] Auto-reconnect failed: {ex.Message}");
                }
            };

            await _connection.StartAsync();
            _isConnected = true;
            
            System.Diagnostics.Debug.WriteLine($"[SignalR] Connected successfully. ConnectionId: {_connection.ConnectionId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SignalR] Connection failed: {ex.Message}");
            _isConnected = false;
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Trennt die Verbindung zum SignalR Hub.
    /// </summary>
    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_connection != null)
            {
                System.Diagnostics.Debug.WriteLine("[SignalR] Disconnecting...");
                await _connection.StopAsync();
                _isConnected = false;
                System.Diagnostics.Debug.WriteLine("[SignalR] Disconnected");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SignalR] Error during disconnect: {ex.Message}");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public bool IsConnected => _isConnected;

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        
        _connectionLock.Dispose();
    }
}

/// <summary>
/// Event-Args für NewVideosScanned Event.
/// </summary>
public class NewVideosScannedEventArgs : EventArgs
{
    public long SourceId { get; }
    public int Count { get; }

    public NewVideosScannedEventArgs(long sourceId, int count)
    {
        SourceId = sourceId;
        Count = count;
    }
}
