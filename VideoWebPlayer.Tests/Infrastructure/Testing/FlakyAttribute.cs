using Xunit.v3;

namespace VideoWebPlayer.Tests.Infrastructure.Testing;

/// <summary>Markiert einen OS-nahen Test als nachweislich umgebungsflaky und damit berechtigt
/// fuer den einmaligen automatischen Retry in der best-effort-Lane. Konvention: nur in
/// Kombination mit <see cref="OsInterfaceAttribute"/> vergeben (abgesichert durch
/// FlakyConventionTests) und nur bei dokumentiertem Flake-Ereignis oder Analyzer-Befund.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class FlakyAttribute : Attribute, ITraitAttribute
{
    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        => [new KeyValuePair<string, string>("Category", TestCategories.Flaky)];
}
