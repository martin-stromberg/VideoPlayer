using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Services;

/// <summary>
/// Resolves the ffprobe executable: configured path first, then convention directories next to the
/// application (<c>tools/ffmpeg/</c> and the application directory itself), then <c>PATH</c>.
/// </summary>
public sealed class FfprobeLocator : IFfprobeLocator
{
    private static readonly string[] ExecutableNames =
        OperatingSystem.IsWindows() ? new[] { "ffprobe.exe", "ffprobe" } : new[] { "ffprobe" };

    private readonly MediaProbeOptions _options;
    private readonly string _baseDirectory;
    private readonly Func<string?> _pathEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfprobeLocator"/> class.
    /// </summary>
    /// <param name="options">Media probe options containing the optional configured path.</param>
    public FfprobeLocator(IOptions<MediaProbeOptions> options)
        : this(options, AppContext.BaseDirectory, () => Environment.GetEnvironmentVariable("PATH"))
    {
    }

    internal FfprobeLocator(IOptions<MediaProbeOptions> options, string baseDirectory, Func<string?> pathEnvironment)
    {
        _options = options.Value;
        _baseDirectory = baseDirectory;
        _pathEnvironment = pathEnvironment;
    }

    /// <inheritdoc />
    public string? Resolve()
    {
        var configured = _options.FfprobePath;
        if (!string.IsNullOrWhiteSpace(configured))
            return File.Exists(configured) ? configured : null;

        foreach (var candidate in ConventionCandidates())
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return ResolveFromPath();
    }

    private IEnumerable<string> ConventionCandidates()
    {
        foreach (var name in ExecutableNames)
        {
            yield return Path.Combine(_baseDirectory, "tools", "ffmpeg", name);
            yield return Path.Combine(_baseDirectory, name);
        }
    }

    private string? ResolveFromPath()
    {
        var pathValue = _pathEnvironment();
        if (string.IsNullOrEmpty(pathValue))
            return null;

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in ExecutableNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory, name);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
