namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Sucht ein externes Werkzeug-Binary (z. B. ffmpeg/ffprobe) über den <c>PATH</c> —
/// gemeinsamer Helfer für E2E-Tests, die echte Werkzeug-Prozesse starten und sich bei
/// fehlendem Binary still beenden.
/// </summary>
internal static class E2EBinaryLocator
{
    /// <summary>Returns the full path of the first <paramref name="name"/> found on
    /// <c>PATH</c>, or <see langword="null"/>.</summary>
    internal static string? Find(string name)
    {
        var exeName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
