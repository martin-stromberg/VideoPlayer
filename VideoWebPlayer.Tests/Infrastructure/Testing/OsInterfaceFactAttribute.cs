using Xunit;
using Xunit.v3;

namespace VideoWebPlayer.Tests.Infrastructure.Testing;

/// <summary>xUnit-Fact fuer Tests mit echter Betriebssystem-Schnittstellen-Beruehrung.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class OsInterfaceFactAttribute : FactAttribute, ITraitAttribute
{
    /// <inheritdoc />
    public OsInterfaceFactAttribute(
        [System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null,
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
    }

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits()
        => [new KeyValuePair<string, string>("Category", TestCategories.OsInterface)];
}
