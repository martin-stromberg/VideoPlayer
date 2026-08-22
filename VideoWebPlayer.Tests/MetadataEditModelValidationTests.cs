using System.ComponentModel.DataAnnotations;
using VideoWebPlayer.Components.Shared.Media;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MetadataEditModelValidationTests
{
    [Fact]
    public void Validate_WhenTitleIsEmpty_ReturnsTitleRequiredMessage()
    {
        var model = new MetadataEditModel { Name = "" };

        var results = Validate(model);

        Assert.Contains(results, result => result.ErrorMessage == "Der Titel darf nicht leer sein.");
    }

    [Fact]
    public void Validate_WhenTitleIsTooLong_ReturnsTitleLengthMessage()
    {
        var model = new MetadataEditModel { Name = new string('x', 513) };

        var results = Validate(model);

        Assert.Contains(results, result => result.ErrorMessage == "Der Titel darf maximal 512 Zeichen lang sein.");
    }

    [Fact]
    public void Validate_WhenPlotIsTooLong_ReturnsPlotLengthMessage()
    {
        var model = new MetadataEditModel
        {
            Name = "Title",
            Plot = new string('x', 10001),
        };

        var results = Validate(model);

        Assert.Contains(results, result => result.ErrorMessage == "Der Plot darf maximal 10000 Zeichen lang sein.");
    }

    private static List<ValidationResult> Validate(MetadataEditModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
