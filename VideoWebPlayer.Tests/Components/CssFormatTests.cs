using System.Globalization;
using VideoWebPlayer.ViewModels;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Direct format assertion for <see cref="CssFormat.Percent"/> (N26): CSS percentages must
/// always serialize with a dot decimal separator — under de-DE a naive
/// <c>double.ToString()</c> produces a comma, which browsers reject so the styled width
/// silently falls back to the element default.
/// </summary>
public sealed class CssFormatTests
{
    [Fact]
    public void Percent_UnderGermanCulture_WritesDotSeparator()
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.Equal("12.5%", CssFormat.Percent(12.5));
            Assert.Equal("59.381461147936385%", CssFormat.Percent(59.381461147936385));
            Assert.Equal("100%", CssFormat.Percent(100));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }
}
