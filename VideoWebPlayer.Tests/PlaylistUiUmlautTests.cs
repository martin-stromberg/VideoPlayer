using System.Text.RegularExpressions;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Regression test for the customer feedback "deutsche Umlaute müssen als solche dargestellt werden ('Öffentlich'
/// darf nicht als 'Oeffentlich' angezeigt werden)": the visible German texts of the playlist feature (Razor
/// components, the sort-mode labels and the messages of the playlist services) must not contain the ASCII
/// transcription (ae/oe/ue/ss instead of ä/ö/ü/ß) of well-known German words.
/// </summary>
/// <remarks>
/// The check is deliberately a curated word list matched on whole words rather than a generic "ae/oe/ue"
/// pattern (which would flag "Queue", "Value", "neue", "Dauer" ...). Comments (Razor <c>@* *@</c>, HTML,
/// <c>//</c>, <c>/* */</c>) and log statements are not part of the visible UI and are ignored; identifiers,
/// CSS classes and routes are English and therefore never match.
/// </remarks>
public class PlaylistUiUmlautTests
{
    private static readonly string[] AsciiTranscribedWords =
    [
        "fuer", "ueber", "zurueck", "zuruecksetzen", "oeffentlich", "oeffentliche", "oeffentlichen", "oeffentlicher",
        "oeffnen", "oeffnet", "loeschen", "geloescht", "uebersicht", "uebersichten", "eintraege", "eintraegen",
        "hinzufuegen", "hinzugefuegt", "unterstuetzt", "unterstuetzte", "groesse", "groesser", "gross", "aendern",
        "aenderung", "aenderungen", "unveraendert", "pruefen", "pruefung", "koennen", "koennte", "muessen",
        "verfuegbar", "verfuegbare", "ungueltig", "ungueltige", "gueltig", "gueltige", "waehlen", "auswaehlen",
        "ausgewaehlt", "naechste", "naechsten", "bestaetigen", "bestaetigung", "bestaetigt", "uebernehmen",
        "uebernommen", "uebersprungen", "ueberschreiben", "zusaetzlich", "tatsaechlich", "moeglichkeit",
        "waehrend", "laeuft", "laedt", "gehoert", "duerfen", "wuerde", "schliessen", "ausschliesslich", "menue",
        "spaeter", "frueh", "spaet", "komoedie", "oberflaeche", "schaltflaechen", "zugaenglich", "zugaengliche",
        "zugaenglicher", "enthaelt"
    ];

    private static readonly Regex AsciiWordRegex = new(
        @"\b(" + string.Join("|", AsciiTranscribedWords) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void PlaylistRazorComponents_ContainNoAsciiTranscribedGermanWords()
    {
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "VideoWebPlayer", "Components", "Playlists"), "*.razor")
            .Append(Path.Combine(root, "VideoWebPlayer", "Components", "Layout", "NavMenu.razor"))
            .ToArray();

        Assert.NotEmpty(files);
        AssertNoAsciiTranscription(files);
    }

    [Fact]
    public void PlaylistClientAndServiceMessages_ContainNoAsciiTranscribedGermanWords()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            Path.Combine(root, "VideoWebPlayer.Client", "Models", "PlaylistSortModeValues.cs"),
            Path.Combine(root, "VideoWebPlayer", "Services", "PlaylistService.cs"),
            Path.Combine(root, "VideoWebPlayer", "Services", "PlaylistEntryReorderService.cs"),
            Path.Combine(root, "VideoWebPlayer", "Services", "PlaylistCover", "PlaylistCoverValidator.cs")
        };

        AssertNoAsciiTranscription(files);
    }

    [Fact]
    public void TranscriptionCheck_DetectsKnownAsciiWords_AndIgnoresCommentsAndCorrectGerman()
    {
        // Guard for the check itself: it must actually catch the customer's example ...
        Assert.NotEmpty(FindAsciiTranscriptions("<h1>Oeffentliche Playlists</h1>"));
        Assert.NotEmpty(FindAsciiTranscriptions("<button title=\"Loeschen\">Zurueck zur Uebersicht</button>"));
        // ... while correct German, English identifiers and comments pass.
        Assert.Empty(FindAsciiTranscriptions("<h1>Öffentliche Playlists</h1><span>Neue Dauer</span> @Queue.Value"));
        Assert.Empty(FindAsciiTranscriptions("@* Oeffentlich: nur ein Kommentar *@<p>Öffentlich</p>"));
        Assert.Empty(FindAsciiTranscriptions("// Loeschen im Kommentar\nvar x = \"Löschen\";"));
        Assert.Empty(FindAsciiTranscriptions("_logger.LogWarning(\"Fehler beim Loeschen\");"));
    }

    private static void AssertNoAsciiTranscription(IEnumerable<string> files)
    {
        var findings = new List<string>();
        foreach (var file in files)
        {
            Assert.True(File.Exists(file), $"Datei nicht gefunden: {file}");
            foreach (var finding in FindAsciiTranscriptions(File.ReadAllText(file)))
                findings.Add($"{Path.GetFileName(file)}: {finding}");
        }

        Assert.True(findings.Count == 0,
            "ASCII-Umschrift statt Umlauten in sichtbaren Texten gefunden:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    private static IEnumerable<string> FindAsciiTranscriptions(string source)
    {
        var withoutBlockComments = Regex.Replace(source, @"@\*.*?\*@|<!--.*?-->|/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        foreach (var rawLine in withoutBlockComments.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                continue;
            if (Regex.IsMatch(line, @"[Ll]ogger\b|\.Log(Information|Warning|Error|Debug|Critical|Trace)\b"))
                continue;

            var code = Regex.Replace(line, @"(?<![:""\w])//.*$", string.Empty);
            foreach (Match match in AsciiWordRegex.Matches(code))
                yield return $"\"{match.Value}\" in: {code.Trim()}";
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VideoPlayer.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (VideoPlayer.sln) not found.");
    }
}
