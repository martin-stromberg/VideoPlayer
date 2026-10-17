using System.ComponentModel.DataAnnotations;
using VideoWebPlayer.Components.Shared;
using VideoWebPlayer.Configuration;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class AbsoluteHttpUrlAttributeTests
{
    private readonly AbsoluteHttpUrlAttribute _attribute = new();

    [Theory]
    [InlineData(null)]
    [InlineData("https://videos.example.com/videoplayer/")]
    [InlineData("http://server.lan:5000/")]
    public void GetValidationResult_AcceptsNullOrAbsoluteHttpUrl(string? value)
    {
        Assert.Equal(
            ValidationResult.Success,
            _attribute.GetValidationResult(value, CreateContext()));
    }

    [Fact]
    public void GetValidationResult_ErrorMessageNamesTheField()
    {
        // In der ValidationSummary fehlt der Feldbezug, wenn die Meldung nur
        // "Der Wert …" sagt — der DisplayName des Feldes muss vorangestellt sein.
        var result = _attribute.GetValidationResult("keine-url", CreateContext());

        Assert.NotNull(result);
        Assert.Equal(
            $"Die öffentliche Basis-URL {DiscoveryUrlRules.PublicBaseUrlRuleText}",
            result!.ErrorMessage);
    }

    private static ValidationContext CreateContext() => new(new object())
    {
        MemberName = "DiscoveryPublicBaseUrl",
        DisplayName = "Die öffentliche Basis-URL",
    };
}
