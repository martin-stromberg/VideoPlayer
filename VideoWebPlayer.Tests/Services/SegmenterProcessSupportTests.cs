using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="SegmenterProcessSupport.StartRealProcess"/>: a failed process start
/// returns <see langword="null"/> AND logs the underlying cause (e.g. a
/// <see cref="System.ComponentModel.Win32Exception"/> „nicht gefunden" vs. „Zugriff
/// verweigert") — the callers' context-free failure message would otherwise stay undiagnosable.
/// </summary>
public sealed class SegmenterProcessSupportTests
{
    [Fact]
    public void StartRealProcess_UnstartableBinary_LogsCauseAndReturnsNull()
    {
        var logger = new ListLogger<object>(new ConcurrentQueue<string>());
        var missing = Path.Combine(Path.GetTempPath(), $"vwp-no-such-tool-{Guid.NewGuid():N}.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = missing,
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var result = SegmenterProcessSupport.StartRealProcess(startInfo, logger, "TestCaller");

        Assert.Null(result);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("TestCaller", entry.Message);
        Assert.Contains(missing, entry.Message);
        Assert.Contains("Exception", entry.Message); // die Startursache ist mitgeloggt
    }
}
