using System.Diagnostics;
using VideoWebPlayer.Tests.Infrastructure.Testing;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>Deterministische Datei-I/O-Tests des best-effort Temp-Verzeichnis-Cleanups.</summary>
public sealed class TempDirectoryCleanupTests
{
    /// <summary>Ein nicht vorhandener Pfad wird toleriert.</summary>
    [Fact]
    public void DeleteBestEffort_MissingDirectory_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-missing-" + Guid.NewGuid().ToString("N"));

        var exception = Record.Exception(() => TempDirectoryCleanup.DeleteBestEffort(root));

        Assert.Null(exception);
    }

    /// <summary>Eine exklusiv geoeffnete Datei laesst alle Loeschversuche scheitern — die Methode
    /// kehrt trotzdem ohne Exception zurueck, damit Cleanup nie einen Assert ueberdeckt.</summary>
    [Fact]
    public void DeleteBestEffort_LockedDirectory_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var lockedFile = Path.Combine(root, "locked.txt");
        File.WriteAllText(lockedFile, "x");
        var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var exception = Record.Exception(() => TempDirectoryCleanup.DeleteBestEffort(root));

            Assert.Null(exception);
        }
        finally
        {
            stream.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>Ein Verzeichnis mit Read-only-Datei wird mit Attribut-Normalisierung vollstaendig entfernt.</summary>
    [Fact]
    public void DeleteBestEffort_ReadOnlyFile_WithNormalize_Deletes()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var readOnlyFile = Path.Combine(root, "readonly.txt");
        File.WriteAllText(readOnlyFile, "x");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        TempDirectoryCleanup.DeleteBestEffort(root, normalizeAttributes: true);

        Assert.False(Directory.Exists(root));
    }

    /// <summary>Das Kill-Sicherheitsnetz beendet registrierte Prozesse vor dem Loeschen;
    /// ein bereits disposed Prozessobjekt wird dabei toleriert. Der langlebige Platzhalter-
    /// prozess ist plattformabhaengig (cmd/ping unter Windows, sleep unter Unix).</summary>
    [OsInterfaceFact]
    public void DeleteBestEffort_KillsRegisteredProcessTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "x.txt"), "x");
        var sleeper = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c ping -n 300 127.0.0.1")
            : new ProcessStartInfo("sleep", "300");
        sleeper.UseShellExecute = false;
        sleeper.CreateNoWindow = true;
        var process = Process.Start(sleeper)!;
        var exitFast = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c exit 0")
            : new ProcessStartInfo("true");
        exitFast.UseShellExecute = false;
        exitFast.CreateNoWindow = true;
        var disposedProcess = Process.Start(exitFast)!;
        disposedProcess.Dispose();
        try
        {
            TempDirectoryCleanup.DeleteBestEffort(root, processesToKill: [process, disposedProcess]);

            Assert.True(process.HasExited, "registrierte Prozesse werden vor dem Loeschen beendet");
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            process.Dispose();
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }

    /// <summary>Eine nicht vorhandene Datei wird toleriert.</summary>
    [Fact]
    public void DeleteFileBestEffort_MissingFile_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-missing-" + Guid.NewGuid().ToString("N") + ".tmp");

        var exception = Record.Exception(() => TempDirectoryCleanup.DeleteFileBestEffort(path));

        Assert.Null(exception);
    }

    /// <summary>Eine exklusiv geoeffnete Datei laesst alle Loeschversuche scheitern — die Methode
    /// kehrt trotzdem ohne Exception zurueck (Windows-Lock-Semantik; unter Unix wird die Datei
    /// einfach geloescht, beides ist best-effort-konform).</summary>
    [Fact]
    public void DeleteFileBestEffort_LockedFile_DoesNotThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(path, "x");
        var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var exception = Record.Exception(() => TempDirectoryCleanup.DeleteFileBestEffort(path));

            Assert.Null(exception);
        }
        finally
        {
            stream.Dispose();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    /// <summary>Eine normale Datei wird geloescht.</summary>
    [Fact]
    public void DeleteFileBestEffort_ExistingFile_Deletes()
    {
        var path = Path.Combine(Path.GetTempPath(), "tempdir-cleanup-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(path, "x");

        TempDirectoryCleanup.DeleteFileBestEffort(path);

        Assert.False(File.Exists(path));
    }
}
