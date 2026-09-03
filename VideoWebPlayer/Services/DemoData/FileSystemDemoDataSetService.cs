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
    private readonly ILogger<FileSystemDemoDataSetService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    public FileSystemDemoDataSetService(
        IWebHostEnvironment env,
        ApplicationDbContext db,
        ILogger<FileSystemDemoDataSetService> logger)
    {
        _env = env;
        _db = db;
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

                var source = new MediaSource
                {
                    Name = name,
                    Host = sourceDef.Host ?? string.Empty,
                    Port = sourceDef.Port ?? 22,
                    Path = sourceDef.Path ?? string.Empty,
                    Username = sourceDef.Username,
                    Password = sourceDef.Password,
                };

                await _db.AddMediaSourceAsync(source);

                _db.MediaSourceUsers.Add(new MediaSourceUser
                {
                    MediaSourceId = source.Id,
                    UserId = user.Id
                });
                await _db.SaveChangesAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
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
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Path { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
