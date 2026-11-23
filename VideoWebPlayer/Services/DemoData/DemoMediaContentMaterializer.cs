using System.Xml.Linq;

namespace VideoWebPlayer.Services.DemoData;

/// <summary>
/// Materializes a demo media directory on disk: copies the shipped template tree
/// (NFO files and other metadata), writes one video file per movie/episode NFO
/// from the shipped dummy video and generates artwork images.
/// </summary>
public sealed class DemoMediaContentMaterializer
{
    private readonly DemoMediaImageGenerator _images;
    private readonly ILogger<DemoMediaContentMaterializer> _logger;

    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="images">The generator used to create the artwork files.</param>
    /// <param name="logger">The logger.</param>
    public DemoMediaContentMaterializer(
        DemoMediaImageGenerator images,
        ILogger<DemoMediaContentMaterializer> logger)
    {
        _images = images;
        _logger = logger;
    }

    /// <summary>
    /// Materializes the given template tree into the target directory. The operation
    /// is idempotent: an existing target directory is left untouched.
    /// </summary>
    /// <param name="contentDirectory">The shipped template tree (absolute path).</param>
    /// <param name="targetDirectory">The writable target directory (absolute path).</param>
    /// <param name="videoFile">The shipped dummy video file (absolute path).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><c>true</c> when the directory was materialized, <c>false</c> when it already existed.</returns>
    public async Task<bool> MaterializeAsync(
        string contentDirectory,
        string targetDirectory,
        string videoFile,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(targetDirectory)
            && Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            _logger.LogInformation("Demo-Medienverzeichnis '{Target}' existiert bereits, Materialisierung wird übersprungen.", targetDirectory);
            return false;
        }

        CopyTree(contentDirectory, targetDirectory, cancellationToken);
        await PopulateFromNfosAsync(targetDirectory, videoFile, cancellationToken);
        _logger.LogInformation("Demo-Medienverzeichnis '{Target}' aus '{Source}' materialisiert.", targetDirectory, contentDirectory);
        return true;
    }

    private static void CopyTree(string sourceDirectory, string targetDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var dir in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, dir)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file));
            File.Copy(file, target, overwrite: true);
        }
    }

    private async Task PopulateFromNfosAsync(string targetDirectory, string videoFile, CancellationToken cancellationToken)
    {
        var videoBytes = await File.ReadAllBytesAsync(videoFile, cancellationToken);
        var nfoFiles = Directory.EnumerateFiles(targetDirectory, "*.nfo", SearchOption.AllDirectories).ToList();
        var seasonDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nfoPath in nfoFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var xml = TryParse(nfoPath);
            if (xml is null)
                continue;

            var directory = Path.GetDirectoryName(nfoPath)!;
            var baseName = Path.GetFileNameWithoutExtension(nfoPath);
            var title = xml.Element("title")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(title))
                title = baseName;

            switch (xml.Name.LocalName)
            {
                case "movie":
                    await WriteVideoAsync(videoBytes, Path.Combine(directory, baseName + ".mp4"), cancellationToken);
                    WriteImage(Path.Combine(directory, baseName + "-poster.jpg"), _images.CreatePoster(title));
                    WriteImage(Path.Combine(directory, baseName + "-fanart.jpg"), _images.CreateFanart(title));
                    break;

                case "episodedetails":
                    await WriteVideoAsync(videoBytes, Path.Combine(directory, baseName + ".mp4"), cancellationToken);
                    WriteImage(Path.Combine(directory, baseName + "-poster.jpg"), _images.CreateEpisodeImage(title));
                    if (int.TryParse(xml.Element("season")?.Value, out var seasonNo) && seasonNo > 0)
                    {
                        var seasonPoster = Path.Combine(directory, $"season{seasonNo:D2}-poster.jpg");
                        if (seasonDirs.Add(seasonPoster))
                            WriteImage(seasonPoster, _images.CreatePoster($"Staffel {seasonNo}"));
                    }
                    break;

                case "tvshow":
                    WriteImage(Path.Combine(directory, "poster.jpg"), _images.CreatePoster(title));
                    WriteImage(Path.Combine(directory, "fanart.jpg"), _images.CreateFanart(title));
                    WriteImage(Path.Combine(directory, "banner.jpg"), _images.CreateBanner(title));
                    break;
            }
        }
    }

    private static XElement? TryParse(string nfoPath)
    {
        try
        {
            var xml = XElement.Parse(File.ReadAllText(nfoPath));
            return xml;
        }
        catch
        {
            return null;
        }
    }

    private static async Task WriteVideoAsync(byte[] videoBytes, string target, CancellationToken cancellationToken)
    {
        if (File.Exists(target))
            return;
        await File.WriteAllBytesAsync(target, videoBytes, cancellationToken);
    }

    private static void WriteImage(string target, byte[] jpegBytes)
    {
        if (File.Exists(target))
            return;
        File.WriteAllBytes(target, jpegBytes);
    }
}
