using System.Diagnostics;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>Best-effort-Loeschung temporaerer Testverzeichnisse: Registrierte Prozesse werden
/// zuerst beendet, danach wird das Verzeichnis mit Retry/Backoff geloescht. Restfehler werden
/// nur diagnostisch ausgegeben statt geworfen, damit ein Cleanup-Fehler nie den eigentlichen
/// Test-Assert ueberdeckt (kein AggregateException-Overlay aus Dispose).</summary>
internal static class TempDirectoryCleanup
{
    private const int MaxDeleteAttempts = 5;
    private static readonly TimeSpan ProcessKillWaitTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Beendet optional registrierte Prozesse und loescht <paramref name="root"/>
    /// best-effort. Wirft nie — ein letzter Restfehler geht nur nach <see cref="Console.Error"/>.</summary>
    /// <param name="root">Zu loeschendes Verzeichnis.</param>
    /// <param name="processesToKill">Optional registrierte Kindprozesse, die vor dem Loeschen
    /// beendet werden (Kill-Sicherheitsnetz; bereits disposed Objekte werden toleriert).</param>
    /// <param name="normalizeAttributes">Dateiattribute vor dem Loeschen auf Normal setzen
    /// (z. B. durch DenyReadAccess/SetUnixFileMode eingeschraenkte Testdateien).</param>
    internal static void DeleteBestEffort(string root, IEnumerable<Process>? processesToKill = null, bool normalizeAttributes = false)
    {
        if (processesToKill is not null)
        {
            foreach (var process in processesToKill)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        if (!process.WaitForExit(ProcessKillWaitTimeout))
                        {
                            Console.Error.WriteLine(
                                $"TempDirectoryCleanup: Prozess {process.Id} lief nach Kill laenger als {ProcessKillWaitTimeout.TotalSeconds}s — Loeschen wird trotzdem fortgesetzt.");
                        }
                    }
                }
                catch (Exception)
                {
                    // Best-effort: bereits disposed (ObjectDisposedException), nicht mehr
                    // vorhanden oder Win32-Fehler duerfen das Cleanup nicht abbrechen.
                }
            }
        }

        try
        {
            if (!Directory.Exists(root))
            {
                return;
            }

            if (normalizeAttributes)
            {
                try
                {
                    // EnumerateFiles wertet lazy aus: Ein unzugaengliches Unterverzeichnis wirft
                    // erst mitten in der Schleife. Der Fehler darf den Loesch-Retry-Loop unten
                    // nicht ueberspringen — Directory.Delete kann trotzdem erfolgreich sein.
                    foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    {
                        try { File.SetAttributes(file, FileAttributes.Normal); }
                        catch (Exception) { }
                    }
                }
                catch (Exception)
                {
                    // Best-effort: Attribut-Normalisierung ist nur Vorbereitung, kein Muss.
                }
            }

            for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                    return;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    if (attempt == MaxDeleteAttempts)
                    {
                        Console.Error.WriteLine(
                            $"TempDirectoryCleanup: '{root}' konnte nach {MaxDeleteAttempts} Versuchen nicht geloescht werden: {error.Message}");
                        return;
                    }
                    Thread.Sleep(100 * attempt);
                }
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"TempDirectoryCleanup: '{root}' konnte nicht geloescht werden: {error.Message}");
        }
    }

    /// <summary>Loescht eine einzelne Datei best-effort mit demselben Retry/Backoff-Protokoll
    /// wie <see cref="DeleteBestEffort"/> (Temp-Datenbanken, Session-Dateien).
    /// Wirft nie — ein letzter Restfehler geht nur nach <see cref="Console.Error"/>.</summary>
    /// <param name="path">Zu loeschende Datei.</param>
    /// <param name="normalizeAttributes">Dateiattribut vor dem Loeschen auf Normal setzen
    /// (z. B. Read-only-markierte Testdateien).</param>
    internal static void DeleteFileBestEffort(string path, bool normalizeAttributes = false)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            if (normalizeAttributes)
            {
                try { File.SetAttributes(path, FileAttributes.Normal); }
                catch (Exception) { }
            }

            for (var attempt = 1; attempt <= MaxDeleteAttempts; attempt++)
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    if (attempt == MaxDeleteAttempts)
                    {
                        Console.Error.WriteLine(
                            $"TempDirectoryCleanup: '{path}' konnte nach {MaxDeleteAttempts} Versuchen nicht geloescht werden: {error.Message}");
                        return;
                    }
                    Thread.Sleep(100 * attempt);
                }
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"TempDirectoryCleanup: '{path}' konnte nicht geloescht werden: {error.Message}");
        }
    }
}
