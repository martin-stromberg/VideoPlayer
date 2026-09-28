using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.FileProviders;

namespace VideoWebPlayer.Services;

/// <summary>
/// Appends a content fingerprint (<c>?v=...</c>) to the URLs of stylesheets and scripts that are referenced without
/// a fingerprint in their file name. A changed file therefore gets a new URL and is fetched again even if a browser
/// still holds an older copy of it in its cache - independent of any <c>Cache-Control</c> header the old copy was
/// served with.
/// </summary>
public class StaticAssetVersioner
{
    private readonly IFileProvider _fileProvider;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticAssetVersioner"/> class.
    /// </summary>
    /// <param name="environment">The hosting environment whose web root provides the static files.</param>
    public StaticAssetVersioner(IWebHostEnvironment environment)
        : this(environment.WebRootFileProvider)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticAssetVersioner"/> class.
    /// </summary>
    /// <param name="fileProvider">The provider the static files are read from.</param>
    public StaticAssetVersioner(IFileProvider fileProvider)
    {
        _fileProvider = fileProvider;
    }

    /// <summary>
    /// Returns the URL of a static file including its fingerprint, or the unchanged path if the file cannot be read.
    /// </summary>
    /// <param name="path">The path relative to the web root, for example <c>app.css</c>.</param>
    /// <returns>The path with a <c>?v=</c> query string, or <paramref name="path"/> if no fingerprint could be built.</returns>
    public string Url(string path)
    {
        var version = _cache.GetOrAdd(path, ComputeVersion);
        return version.Length == 0 ? path : $"{path}?v={version}";
    }

    private string ComputeVersion(string path)
    {
        try
        {
            var file = _fileProvider.GetFileInfo(path);
            if (!file.Exists)
                return string.Empty;

            using var stream = file.CreateReadStream();
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
