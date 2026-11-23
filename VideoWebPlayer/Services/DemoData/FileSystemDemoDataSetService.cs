using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services.DemoData;

/// <summary>
/// Loads demo data set definitions from JSON files on disk and applies them to the database.
/// </summary>
public sealed class FileSystemDemoDataSetService : IDemoDataSetService
{
    private const string DemoDataFolderName = "DemoDataSets";
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;
    private readonly DemoMediaContentMaterializer _materializer;
    private readonly MediaSourceScanCoordinator _scanCoordinator;
    private readonly ILogger<FileSystemDemoDataSetService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="env">The host environment used to resolve the demo data folder.</param>
    /// <param name="db">The database context.</param>
    /// <param name="materializer">The materializer for local demo media content.</param>
    /// <param name="scanCoordinator">The scan coordinator used to start the initial scan.</param>
    /// <param name="logger">The logger.</param>
    public FileSystemDemoDataSetService(
        IWebHostEnvironment env,
        ApplicationDbContext db,
        DemoMediaContentMaterializer materializer,
        MediaSourceScanCoordinator scanCoordinator,
        ILogger<FileSystemDemoDataSetService> logger)
    {
        _env = env;
        _db = db;
        _materializer = materializer;
        _scanCoordinator = scanCoordinator;
        _logger = logger;
        _jsonOptions = new(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DemoDataSetInfo>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var folder = GetDemoDataFolderPath();
        if (folder is null || !Directory.Exists(folder))
            return Array.Empty<DemoDataSetInfo>();

        var result = new List<DemoDataSetInfo>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var def = await JsonSerializer.DeserializeAsync<DemoDataSetDefinition>(stream, _jsonOptions, cancellationToken);
                if (def is null || string.IsNullOrWhiteSpace(def.Name))
                    continue;

                var id = Path.GetFileNameWithoutExtension(file);
                result.Add(new DemoDataSetInfo(id, def.Name.Trim(), string.IsNullOrWhiteSpace(def.Description) ? null : def.Description.Trim()));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Konnte Demodatenbestand '{File}' nicht laden.", file);
            }
        }

        return result
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <inheritdoc />
    public async Task ApplyAsync(string demoDataSetId, ApplicationUser user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(demoDataSetId))
            return;

        var folder = GetDemoDataFolderPath();
        if (folder is null || !Directory.Exists(folder))
            throw new DirectoryNotFoundException($"Demodaten-Verzeichnis nicht gefunden: '{folder}'.");

        var file = Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), demoDataSetId, StringComparison.OrdinalIgnoreCase));

        if (file is null)
            throw new FileNotFoundException($"Demodatenbestand '{demoDataSetId}' nicht gefunden.");

        DemoDataSetDefinition? def;
        await using (var stream = File.OpenRead(file))
        {
            def = await JsonSerializer.DeserializeAsync<DemoDataSetDefinition>(stream, _jsonOptions, cancellationToken);
        }

        if (def is null)
            throw new InvalidOperationException($"Demodatenbestand '{demoDataSetId}' konnte nicht gelesen werden.");

        var anySourceCreated = false;
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var sourceDef in def.MediaSources ?? new List<DemoMediaSourceDefinition>())
            {
                if (string.IsNullOrWhiteSpace(sourceDef.Name))
                    continue;

                var name = sourceDef.Name.Trim();
                var exists = await _db.MediaSources.AnyAsync(s => s.Name == name, cancellationToken);
                if (exists)
                    continue;

                var sourceType = ParseSourceType(sourceDef.SourceType, demoDataSetId, name);
                var source = new MediaSource
                {
                    Name = name,
                    SourceType = sourceType,
                    Host = sourceDef.Host ?? string.Empty,
                    Port = sourceDef.Port ?? 22,
                    Path = sourceDef.Path ?? string.Empty,
                    Username = sourceDef.Username,
                    Password = sourceDef.Password,
                };

                if (sourceType == MediaSourceType.LocalDirectory)
                {
                    var targetPath = ResolveLocalPath(sourceDef.Path, demoDataSetId);
                    if (!string.IsNullOrWhiteSpace(sourceDef.ContentDirectory))
                    {
                        await _materializer.MaterializeAsync(
                            ResolveDemoDataPath(folder, sourceDef.ContentDirectory, demoDataSetId, name, mustExist: true),
                            targetPath,
                            ResolveDemoDataPath(folder, RequireVideoFile(sourceDef, demoDataSetId, name), demoDataSetId, name, mustExist: true),
                            cancellationToken);
                    }
                    source.Path = targetPath;
                    source.Host = string.Empty;
                    source.Port = 0;
                    source.Username = null;
                    source.Password = null;
                }

                await _db.AddMediaSourceAsync(source);

                _db.MediaSourceUsers.Add(new MediaSourceUser
                {
                    MediaSourceId = source.Id,
                    UserId = user.Id
                });
                await _db.SaveChangesAsync(cancellationToken);
                anySourceCreated = true;
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }

        if (anySourceCreated)
            await _scanCoordinator.RunFullScanAsync("Demodaten-Scan", null, cancellationToken);
    }

    private static MediaSourceType ParseSourceType(string? value, string demoDataSetId, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return MediaSourceType.Sftp;
        if (Enum.TryParse<MediaSourceType>(value.Trim(), ignoreCase: true, out var parsed))
            return parsed;
        throw new InvalidOperationException(
            $"Demodatenbestand '{demoDataSetId}': Unbekannter Quelltyp '{value}' für Quelle '{sourceName}'.");
    }

    private string ResolveLocalPath(string? path, string demoDataSetId)
    {
        var effectivePath = string.IsNullOrWhiteSpace(path)
            ? Path.Combine("Data", "DemoMedia", demoDataSetId)
            : path.Trim();
        if (Path.IsPathRooted(effectivePath))
            return Path.GetFullPath(effectivePath);
        return Path.GetFullPath(Path.Combine(_env.ContentRootPath, effectivePath));
    }

    private static string ResolveDemoDataPath(string demoDataFolder, string relativePath, string demoDataSetId, string sourceName, bool mustExist)
    {
        var fullPath = Path.GetFullPath(Path.Combine(demoDataFolder, relativePath.Trim()));
        var normalizedRoot = Path.GetFullPath(demoDataFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Demodatenbestand '{demoDataSetId}': Pfad '{relativePath}' für Quelle '{sourceName}' liegt außerhalb des Bestands.");
        if (mustExist && !File.Exists(fullPath) && !Directory.Exists(fullPath))
            throw new InvalidOperationException(
                $"Demodatenbestand '{demoDataSetId}': '{relativePath}' für Quelle '{sourceName}' wurde nicht gefunden.");
        return fullPath;
    }

    private static string RequireVideoFile(DemoMediaSourceDefinition sourceDef, string demoDataSetId, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceDef.VideoFile))
            throw new InvalidOperationException(
                $"Demodatenbestand '{demoDataSetId}': Quelle '{sourceName}' mit 'contentDirectory' benötigt 'videoFile'.");
        return sourceDef.VideoFile;
    }

    private string? GetDemoDataFolderPath()
    {
        // Development: ContentRootPath points to project directory.
        var candidate = Path.Combine(_env.ContentRootPath, DemoDataFolderName);
        if (Directory.Exists(candidate))
            return candidate;

        // Published output: content is typically next to the executable.
        candidate = Path.Combine(AppContext.BaseDirectory, DemoDataFolderName);
        if (Directory.Exists(candidate))
            return candidate;

        return Path.Combine(_env.ContentRootPath, DemoDataFolderName);
    }

    private sealed class DemoDataSetDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<DemoMediaSourceDefinition>? MediaSources { get; set; }
    }

    private sealed class DemoMediaSourceDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? SourceType { get; set; }
        public string? ContentDirectory { get; set; }
        public string? VideoFile { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Path { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
