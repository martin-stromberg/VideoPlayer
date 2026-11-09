using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="FfmpegLocator"/>: configured path first, then the convention directories
/// (<c>tools/ffmpeg/</c> and the application directory itself), then <c>PATH</c>.
/// </summary>
public sealed class FfmpegLocatorTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"vwp-ffmpeg-locator-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string Touch(params string[] segments)
    {
        var path = Path.Combine(new[] { _tempDir }.Concat(segments).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "fake");
        return path;
    }

    private static FfmpegLocator CreateLocator(MediaProbeOptions options, string baseDir, Func<string?> pathEnv)
        => new(Options.Create(options), baseDir, pathEnv);

    [Fact]
    public void Resolve_ConfiguredPathExists_ReturnsConfigured()
    {
        var exe = Touch("bin", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var locator = CreateLocator(new MediaProbeOptions { FfmpegPath = exe }, _tempDir, () => null);

        Assert.Equal(exe, locator.Resolve());
    }

    [Fact]
    public void Resolve_ConfiguredPathMissing_ReturnsNull()
    {
        var locator = CreateLocator(
            new MediaProbeOptions { FfmpegPath = Path.Combine(_tempDir, "missing", "ffmpeg") },
            _tempDir, () => Path.Combine(_tempDir, "none"));

        Assert.Null(locator.Resolve());
    }

    [Fact]
    public void Resolve_ConventionToolsDirectory_ReturnsCandidate()
    {
        var exe = Touch("tools", "ffmpeg", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var locator = CreateLocator(new MediaProbeOptions(), _tempDir, () => null);

        Assert.Equal(exe, locator.Resolve());
    }

    [Fact]
    public void Resolve_PathEnvironment_ReturnsMatch()
    {
        var exe = Touch("pathdir", OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        var locator = CreateLocator(new MediaProbeOptions(), _tempDir,
            () => Path.Combine(_tempDir, "pathdir"));

        Assert.Equal(exe, locator.Resolve());
    }

    [Fact]
    public void Resolve_NothingFound_ReturnsNull()
    {
        var locator = CreateLocator(new MediaProbeOptions(), _tempDir, () => string.Empty);

        Assert.Null(locator.Resolve());
    }
}
