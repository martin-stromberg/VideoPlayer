using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using VideoWebPlayer.Components.Shared.Media;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// bUnit tests for <see cref="AgeRatingSelect"/>: canonical FSK/MPAA options plus the
/// extra option that keeps a stored non-canonical value (e.g. „TV-MA" from an NFO)
/// visible and selectable.
/// </summary>
public class AgeRatingSelectTests
{
    [Fact]
    public void CanonicalValue_RendersOnlyStandardOptions()
    {
        using var ctx = new global::Bunit.BunitContext();
        var model = new MetadataEditModel { AgeRating = "FSK 12" };

        var cut = RenderSelect(ctx, model);

        var values = OptionValues(cut);
        Assert.Equal(
            new[] { "", "FSK 0", "FSK 6", "FSK 12", "FSK 16", "FSK 18", "G", "PG", "PG-13", "R", "NC-17" },
            values);
    }

    [Fact]
    public void NonCanonicalStoredValue_RendersAdditionalOption()
    {
        using var ctx = new global::Bunit.BunitContext();
        var model = new MetadataEditModel { AgeRating = "TV-MA" };

        var cut = RenderSelect(ctx, model);

        var values = OptionValues(cut);
        Assert.Equal(12, values.Count);
        Assert.Equal("TV-MA", values.Last());
        Assert.Equal("TV-MA", cut.Instance.Value);
    }

    [Fact]
    public void EmptyValue_RendersOnlyStandardOptions()
    {
        using var ctx = new global::Bunit.BunitContext();
        var model = new MetadataEditModel { AgeRating = "" };

        var cut = RenderSelect(ctx, model);

        Assert.Equal(11, OptionValues(cut).Count);
    }

    private static IRenderedComponent<AgeRatingSelect> RenderSelect(
        global::Bunit.BunitContext ctx, MetadataEditModel model)
    {
        var editContext = new EditContext(model);
        return ctx.Render<AgeRatingSelect>(parameters => parameters
            .AddCascadingValue(editContext)
            .Add(p => p.Value, model.AgeRating)
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string>(model, v => model.AgeRating = v))
            .Add(p => p.ValueExpression, (Expression<Func<string>>)(() => model.AgeRating)));
    }

    private static List<string?> OptionValues(IRenderedComponent<AgeRatingSelect> cut)
        => cut.FindAll("option").Select(o => o.GetAttribute("value")).ToList();
}
