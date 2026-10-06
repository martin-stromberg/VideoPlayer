using System.ComponentModel.DataAnnotations;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Components.Shared;

/// <summary>
/// Validates that a value is either empty (means "unset") or an absolute
/// <c>http</c>/<c>https</c> URL. The shared rule lives in
/// <see cref="DiscoveryUrlRules"/> so the form, the startup options validation and the
/// server-side persistence check apply the same criterion. The error message is
/// prefixed with the field's display name (<c>[Display]</c> or member name) so it
/// stays attributable in the <c>ValidationSummary</c>.
/// </summary>
public sealed class AbsoluteHttpUrlAttribute : ValidationAttribute
{
    /// <summary>
    /// Creates a new instance with the default error message; <c>{0}</c> is replaced
    /// by the display name of the validated field.
    /// </summary>
    public AbsoluteHttpUrlAttribute()
        : base($"{{0}} {DiscoveryUrlRules.PublicBaseUrlRuleText}")
    {
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;
        if (value is string text && DiscoveryUrlRules.IsValidPublicBaseUrl(text))
            return ValidationResult.Success;

        return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
    }
}
