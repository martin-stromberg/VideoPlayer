using Xunit.v3;

namespace VideoWebPlayer.Tests.Infrastructure.Testing;

/// <summary>Markiert einen Test oder eine Testklasse als OS-Schnittstellen-Test.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class OsInterfaceAttribute : Attribute, ITraitAttribute
{
    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        => [new KeyValuePair<string, string>("Category", TestCategories.OsInterface)];
}
