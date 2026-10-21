using System.Net;

namespace VideoWebPlayer.ViewModels;

/// <summary>
/// Senderbezogene, verständliche Meldungen der Live-TV-Ansicht. Gleiches Muster wie
/// <see cref="MediaAccessErrorMessages"/>, aber auf den TV-Sender zugeschnitten: Die Ursache ist
/// hier jeweils bekannt (kein laufender Slot laut Programmauskunft, nicht ladbarer Stream,
/// fehlende Browser-Unterstützung), daher sind konkrete statt generischer Texte möglich.
/// </summary>
internal static class LiveTvErrorMessages
{
    /// <summary>Message shown while the schedule gap lasts — no broadcast is currently airing.</summary>
    public const string NoProgramRunning = "Aktuell läuft keine Sendung.";

    /// <summary>Message shown when the stream itself cannot be loaded; retried automatically.</summary>
    public const string StreamUnavailable =
        "Der Live-Stream ist momentan nicht verfügbar. Es wird automatisch erneut versucht.";

    /// <summary>Message shown when the browser can play neither native HLS nor hls.js over MSE.</summary>
    public const string UnsupportedBrowser =
        "Live-TV wird von diesem Browser leider nicht unterstützt.";

    /// <summary>Message shown when the program guide could not be loaded at all.</summary>
    public const string LoadFailed =
        "Die Programminformationen konnten nicht geladen werden. Bitte versuchen Sie es später erneut.";

    /// <summary>Message shown when the server answers 403 Forbidden for the program data.</summary>
    public const string Forbidden = "Sie haben keinen Zugriff auf den TV-Sender.";

    /// <summary>
    /// Returns the user-facing message for <paramref name="exception"/> if it is a final 403
    /// answer, otherwise <see langword="null"/> so the caller keeps <see cref="LoadFailed"/>.
    /// </summary>
    /// <param name="exception">The exception a client call failed with.</param>
    /// <returns>The message to display, or <see langword="null"/> if the exception is not a 403 answer.</returns>
    public static string? Describe(Exception exception)
        => exception is HttpRequestException { StatusCode: HttpStatusCode.Forbidden }
            ? Forbidden
            : null;
}
