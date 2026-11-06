using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests the ffprobe path resolution order of <see cref="FfprobeLocator"/>.
/// </summary>
public class FfprobeLocatorTests : IDisposable
{
    private readonly string _baseDir;
    private readonly string _pathDir;

    public FfprobeLocatorTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"vwp-locator-base-{Guid.NewGuid():N}");
        _pathDir = Path.Combine(Path.GetTempPath(), $"vwp-locator-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_baseDir);
        Directory.CreateDirectory(_pathDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_baseDir, recursive: true); } catch { }
        try { Directory.Delete(_pathDir, recursive: true); } catch { }
    }

    [Fact]
    public void Resolve_ConfiguredPath_Wins()
    {
        var configured = Path.Combine(_pathDir, "ffprobe.exe");
        File.WriteAllText(configured, "stub");
        var conventionDir = Path.Combine(_baseDir, "tools", "ffmpeg");
        Directory.CreateDirectory(conventionDir);
        File.WriteAllText(Path.Combine(conventionDir, "ffprobe.exe"), "stub");

        var locator = CreateLocator(new MediaProbeOptions { FfprobePath = configured });

        Assert.Equal(configured, locator.Resolve());
    }

    [Fact]
    public void Resolve_ConventionDirectory_IsUsed()
    {
        var conventionDir = Path.Combine(_baseDir, "tools", "ffmpeg");
        Directory.CreateDirectory(conventionDir);
        var expected = Path.Combine(conventionDir, ExecutableName());
        File.WriteAllText(expected, "stub");

        var locator = CreateLocator(new MediaProbeOptions());

        Assert.Equal(expected, locator.Resolve());
    }

    [Fact]
    public void Resolve_PathEnvironment_IsFallback()
    {
        var expected = Path.Combine(_pathDir, ExecutableName());
        File.WriteAllText(expected, "stub");

        var locator = new FfprobeLocator(
            Options.Create(new MediaProbeOptions()),
            _baseDir,
            () => _pathDir);

        Assert.Equal(expected, locator.Resolve());
    }

    [Fact]
    public void Resolve_NotFound_ReturnsNull()
    {
        var locator = new FfprobeLocator(
            Options.Create(new MediaProbeOptions()),
            _baseDir,
            () => _pathDir);

        Assert.Null(locator.Resolve());
    }

    [Fact]
    public void Resolve_ConfiguredPathMissing_ReturnsNull()
    {
        var existing = Path.Combine(_baseDir, ExecutableName());
        File.WriteAllText(existing, "stub");

        var locator = new FfprobeLocator(
            Options.Create(new MediaProbeOptions { FfprobePath = Path.Combine(_baseDir, "missing", "ffprobe.exe") }),
            _baseDir,
            () => null);

        Assert.Null(locator.Resolve());
    }

    private FfprobeLocator CreateLocator(MediaProbeOptions options)
        => new(Options.Create(options), _baseDir, () => _pathDir);

    private static string ExecutableName()
        => OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
}
