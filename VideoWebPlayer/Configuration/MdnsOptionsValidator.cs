using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace VideoWebPlayer.Configuration
{
    /// <summary>
    /// Validates <see cref="MdnsOptions"/> after configuration binding.
    /// </summary>
    public sealed class MdnsOptionsValidator : IValidateOptions<MdnsOptions>
    {
        // DNS-SD convention: "_<label>._tcp" or "_<label>._udp", optionally followed by
        // ".local" or ".local.". The label allows lowercase letters, digits and hyphens,
        // is limited to 15 characters and must not begin or end with a hyphen (RFC 6335).
        private static readonly Regex ServiceTypePattern = new(
            @"^_[a-z0-9](?:[a-z0-9-]{0,13}[a-z0-9])?\._(?:tcp|udp)(?:\.local\.?)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, MdnsOptions options)
        {
            if (options.Port is not null && (options.Port < 1 || options.Port > 65535))
                return ValidateOptionsResult.Fail("Mdns:Port muss zwischen 1 und 65535 liegen.");

            if (string.IsNullOrWhiteSpace(options.ServiceType) || !ServiceTypePattern.IsMatch(options.ServiceType))
                return ValidateOptionsResult.Fail(
                    "Mdns:ServiceType muss dem Muster '_{label}._tcp' bzw. '_{label}._udp' entsprechen (Label: Kleinbuchstaben/Ziffern, Bindestrich nur dazwischen, max. 15 Zeichen, optional mit Suffix '.local').");

            if (options.Enabled)
            {
                if (string.IsNullOrWhiteSpace(options.InstanceName))
                    return ValidateOptionsResult.Fail("Mdns:InstanceName darf nicht leer sein, wenn Mdns:Enabled aktiv ist.");
                if (options.InstanceName.Length > 63)
                    return ValidateOptionsResult.Fail("Mdns:InstanceName darf höchstens 63 Zeichen lang sein.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
