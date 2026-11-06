using System.Xml.Linq;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class TVShowAgeRatingTests
{
    [Fact]
    public void LoadFromXml_SetsAgeRating_FromMpaaElement()
    {
        var show = new TVShow();
        show.LoadFromXml(XElement.Parse("<tvshow><title>Test</title><mpaa>FSK 16</mpaa></tvshow>"));

        Assert.Equal("FSK 16", show.AgeRatingRaw);
        Assert.Equal("FSK 16", show.AgeRating);
        Assert.Equal(16, show.AgeRatingMinAge);
    }

    [Fact]
    public void LoadFromXml_SetsAgeRating_FromCertificationFallback()
    {
        var show = new TVShow();
        show.LoadFromXml(XElement.Parse("<tvshow><title>Test</title><certification>Germany:12</certification></tvshow>"));

        Assert.Equal("Germany:12", show.AgeRatingRaw);
        Assert.Equal("FSK 12", show.AgeRating);
        Assert.Equal(12, show.AgeRatingMinAge);
    }

    [Fact]
    public void LoadFromXml_WithoutRatingElement_LeavesAllFieldsNull()
    {
        var show = new TVShow();
        show.LoadFromXml(XElement.Parse("<tvshow><title>Test</title></tvshow>"));

        Assert.Null(show.AgeRatingRaw);
        Assert.Null(show.AgeRating);
        Assert.Null(show.AgeRatingMinAge);
    }
}
