using SQLite;
using VideoWebPlayer.Maui.Models;

namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// Shared SQLite database for the application.
/// Provides a single connection used by DownloadManager and ElementCacheService.
/// </summary>
public class ClientDatabase
{
    private static ClientDatabase? _instance;
    public static ClientDatabase Instance => _instance ??= new ClientDatabase();

    internal SQLiteAsyncConnection Database { get; }
    internal SemaphoreSlim Lock { get; } = new(1, 1);

    private ClientDatabase()
    {
        var dbFileName = ProfileManager.Instance.GetDatabaseFileName();
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, dbFileName);
        Database = new SQLiteAsyncConnection(dbPath);
        Database.CreateTableAsync<DownloadedVideo>().Wait();
        Database.CreateTableAsync<CachedCarouselItem>().Wait();

        System.Diagnostics.Debug.WriteLine($"[ClientDatabase] Initialized: {dbPath}");
    }
}
