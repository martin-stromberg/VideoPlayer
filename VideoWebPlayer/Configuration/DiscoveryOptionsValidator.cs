using Microsoft.Extensions.Options;

namespace VideoWebPlayer.Configuration
{
    /// <summary>
    /// Validates <see cref="DiscoveryOptions"/> after configuration binding.
    /// </summary>
    public sealed class DiscoveryOptionsValidator : IValidateOptions<DiscoveryOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, DiscoveryOptions options)
        {
            if (!DiscoveryUrlRules.IsValidPublicBaseUrl(options.PublicBaseUrl))
                return ValidateOptionsResult.Fail(
                    $"Discovery:PublicBaseUrl {DiscoveryUrlRules.PublicBaseUrlRuleText}");

            return ValidateOptionsResult.Success;
        }
    }
}
