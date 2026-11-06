using System.Globalization;

namespace VideoWebPlayer.ViewModels;

/// <summary>
/// Formatting helpers for values embedded into CSS. CSS is always parsed with invariant
/// (dot-decimal) number syntax — a <see cref="double"/> rendered through Razor's default
/// <c>ToString()</c> picks up the UI culture and produces e.g. <c>width:59,38%</c> under
/// de-DE, which the browser discards so the styled element keeps its full/default width.
/// </summary>
internal static class CssFormat
{
    /// <summary>
    /// Formats <paramref name="percent"/> as a CSS percentage (<c>"59.38%"</c>) using the
    /// invariant culture — safe to embed in <c>style="width:…"</c> attributes.
    /// </summary>
    /// <param name="percent">The percentage value (0–100 expected; callers may clamp first).</param>
    /// <returns>The percentage followed by a percent sign, formatted invariantly.</returns>
    public static string Percent(double percent)
        => percent.ToString(CultureInfo.InvariantCulture) + "%";
}
