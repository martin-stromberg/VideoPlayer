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
}
