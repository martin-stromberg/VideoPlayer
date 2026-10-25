using System.Xml.Linq;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MovieAgeRatingTests
{
    [Fact]
    public void LoadFromXml_SetsAgeRating_FromMpaaElement()
    {
        var movie = new Movie();
        movie.LoadFromXml(XElement.Parse("<movie><title>Test</title><mpaa>FSK 12</mpaa></movie>"));

        Assert.Equal("FSK 12", movie.AgeRatingRaw);
        Assert.Equal("FSK 12", movie.AgeRating);
        Assert.Equal(12, movie.AgeRatingMinAge);
    }

    [Fact]
    public void LoadFromXml_SetsAgeRating_FromCertificationFallback()
    {
        var movie = new Movie();
        movie.LoadFromXml(XElement.Parse("<movie><title>Test</title><certification>Rated PG-13</certification></movie>"));

        Assert.Equal("Rated PG-13", movie.AgeRatingRaw);
        Assert.Equal("PG-13", movie.AgeRating);
        Assert.Equal(13, movie.AgeRatingMinAge);
    }

    [Fact]
    public void LoadFromXml_MpaaElementTakesPriorityOverCertification()
    {
        var movie = new Movie();
        movie.LoadFromXml(XElement.Parse("<movie><mpaa>FSK 18</mpaa><certification>Rated PG-13</certification></movie>"));

        Assert.Equal("FSK 18", movie.AgeRatingRaw);
        Assert.Equal("FSK 18", movie.AgeRating);
        Assert.Equal(18, movie.AgeRatingMinAge);
    }

    [Fact]
    public void LoadFromXml_WithoutRatingElement_LeavesAllFieldsNull()
    {
        var movie = new Movie();
        movie.LoadFromXml(XElement.Parse("<movie><title>Test</title></movie>"));

        Assert.Null(movie.AgeRatingRaw);
        Assert.Null(movie.AgeRating);
        Assert.Null(movie.AgeRatingMinAge);
    }
}
