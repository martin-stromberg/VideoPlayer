using System.Xml.Linq;
using VideoWebPlayer.Utils;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class AgeRatingNormalizerTests
{
    [Theory]
    [InlineData("FSK 12", "FSK 12", 12)]
    [InlineData("FSK12", "FSK 12", 12)]
    [InlineData("FSK 0", "FSK 0", 0)]
    [InlineData("FSK 6", "FSK 6", 6)]
    [InlineData("FSK 16", "FSK 16", 16)]
    [InlineData("FSK 18", "FSK 18", 18)]
    [InlineData("fsk 12", "FSK 12", 12)]
    [InlineData("Rated PG-13", "PG-13", 13)]
    [InlineData("PG-13", "PG-13", 13)]
    [InlineData("Rated R", "R", 17)]
    [InlineData("NC-17", "NC-17", 18)]
    [InlineData("Rated G", "G", 0)]
    [InlineData("PG", "PG", 8)]
    [InlineData("Germany:16", "FSK 16", 16)]
    [InlineData("DE:6", "FSK 6", 6)]
    [InlineData("US:PG-13", "PG-13", 13)]
    public void Normalize_KnownValues_ReturnsCanonicalValueAndMinAge(string raw, string expectedDisplay, int expectedMinAge)
    {
        var result = AgeRatingNormalizer.Normalize(raw);

        Assert.Equal(expectedDisplay, result.DisplayValue);
        Assert.Equal(expectedMinAge, result.MinAge);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyInput_ReturnsNulls(string? raw)
    {
        var result = AgeRatingNormalizer.Normalize(raw);

        Assert.Null(result.DisplayValue);
        Assert.Null(result.MinAge);
    }

    [Theory]
    [InlineData("TV-14", "TV-14")]
    [InlineData("Unbekannt", "Unbekannt")]
    [InlineData("FSK 10", "FSK 10")]
    [InlineData("  Rated X  ", "Rated X")]
    public void Normalize_UnknownValues_FallsBackToTrimmedRaw(string raw, string expectedDisplay)
    {
        var result = AgeRatingNormalizer.Normalize(raw);

        Assert.Equal(expectedDisplay, result.DisplayValue);
        Assert.Null(result.MinAge);
    }

    [Fact]
    public void ExtractFromXml_MpaaElementWinsOverCertification()
    {
        var xml = XElement.Parse("<movie><mpaa>FSK 18</mpaa><certification>Rated PG-13</certification></movie>");

        var (raw, rating) = AgeRatingNormalizer.ExtractFromXml(xml);

        Assert.Equal("FSK 18", raw);
        Assert.Equal("FSK 18", rating.DisplayValue);
        Assert.Equal(18, rating.MinAge);
    }

    [Fact]
    public void ExtractFromXml_FallsBackToCertification()
    {
        var xml = XElement.Parse("<tvshow><mpaa>   </mpaa><certification>Rated PG-13</certification></tvshow>");

        var (raw, rating) = AgeRatingNormalizer.ExtractFromXml(xml);

        Assert.Equal("Rated PG-13", raw);
        Assert.Equal("PG-13", rating.DisplayValue);
        Assert.Equal(13, rating.MinAge);
    }

    [Fact]
    public void ExtractFromXml_NoRatingElement_ReturnsNulls()
    {
        var xml = XElement.Parse("<movie><title>Test</title></movie>");

        var (raw, rating) = AgeRatingNormalizer.ExtractFromXml(xml);

        Assert.Null(raw);
        Assert.Null(rating.DisplayValue);
        Assert.Null(rating.MinAge);
    }

    [Fact]
    public void CanonicalOptions_ContainsAllKnownStepsInDisplayOrder()
    {
        Assert.Equal(
            new[]
            {
                ("FSK 0", "FSK 0"),
                ("FSK 6", "FSK 6"),
                ("FSK 12", "FSK 12"),
                ("FSK 16", "FSK 16"),
                ("FSK 18", "FSK 18"),
                ("G", "G (MPAA)"),
                ("PG", "PG (MPAA)"),
                ("PG-13", "PG-13 (MPAA)"),
                ("R", "R (MPAA)"),
                ("NC-17", "NC-17 (MPAA)"),
            },
            AgeRatingNormalizer.CanonicalOptions);
    }
}
