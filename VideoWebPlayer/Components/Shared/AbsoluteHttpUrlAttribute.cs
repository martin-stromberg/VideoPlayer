using System.ComponentModel.DataAnnotations;
using VideoWebPlayer.Configuration;

namespace VideoWebPlayer.Components.Shared;

/// <summary>
/// Validates that a value is either empty (means "unset") or an absolute
/// <c>http</c>/<c>https</c> URL. The shared rule lives in
/// <see cref="DiscoveryUrlRules"/> so the form, the startup options validation and the
/// server-side persistence check apply the same criterion.
/// </summary>
public sealed class AbsoluteHttpUrlAttribute : ValidationAttribute
{
    /// <summary>
    /// Creates a new instance with the default error message.
    /// </summary>
    public AbsoluteHttpUrlAttribute()
        : base($"Der Wert {DiscoveryUrlRules.PublicBaseUrlRuleText}")
    {
    }

    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;
        if (value is string text && DiscoveryUrlRules.IsValidPublicBaseUrl(text))
            return ValidationResult.Success;

        return new ValidationResult(ErrorMessage);
    }
}
