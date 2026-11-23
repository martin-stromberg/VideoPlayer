using System.Reflection;
using Xunit;
using Xunit.v3;

namespace VideoWebPlayer.Tests.Infrastructure.Testing;

/// <summary>Sichert die Lane-Matrix ab: <see cref="FlakyAttribute"/> und der E2E-Trait duerfen
/// nur in Kombination mit <see cref="OsInterfaceAttribute"/> vergeben werden — ein Verstoss
/// wuerde einen retry-berechtigten bzw. umgebungssensitiven Test in die blockierende Lane
/// schieben. Der Test selbst traegt keinen Trait und laeuft in der blockierenden Lane.</summary>
public sealed class FlakyConventionTests
{
    /// <summary>Jede Methode oder Klasse, deren Trait-Attribute Category=Flaky liefern,
    /// muss zusaetzlich Category=OsInterface liefern.</summary>
    [Fact]
    public void FlakyMarkedTests_AlwaysCarryOsInterface()
    {
        var violations = FindViolations(TestCategories.Flaky);

        Assert.True(violations.Count == 0,
            $"Flaky darf nur in Kombination mit OsInterface vergeben werden: {string.Join(", ", violations)}");
    }

    /// <summary>Jede Methode oder Klasse, deren Trait-Attribute Category=E2E liefern,
    /// muss zusaetzlich Category=OsInterface liefern — sonst landet ein Browser-/Server-
    /// Test still in der blockierenden regulaeren Lane.</summary>
    [Fact]
    public void E2eMarkedTests_AlwaysCarryOsInterface()
    {
        var violations = FindViolations(TestCategories.E2E);

        Assert.True(violations.Count == 0,
            $"E2E muss immer zusammen mit OsInterface vergeben werden: {string.Join(", ", violations)}");
    }

    private static List<string> FindViolations(string requiredCompanionCategory)
    {
        var violations = new List<string>();
        var assembly = typeof(FlakyConventionTests).Assembly;
        foreach (var type in assembly.GetTypes())
        {
            var typeTraits = Traits(type.GetCustomAttributes(inherit: true)).ToArray();
            if (HasCategory(typeTraits, requiredCompanionCategory) && !HasCategory(typeTraits, TestCategories.OsInterface))
            {
                violations.Add(type.FullName ?? type.Name);
            }
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var methodTraits = Traits(method.GetCustomAttributes(inherit: true)).Concat(typeTraits).ToArray();
                if (HasCategory(methodTraits, requiredCompanionCategory) && !HasCategory(methodTraits, TestCategories.OsInterface))
                {
                    violations.Add($"{type.FullName}.{method.Name}");
                }
            }
        }
        return violations;
    }

    private static IEnumerable<KeyValuePair<string, string>> Traits(IEnumerable<object> attributes)
        => attributes.OfType<ITraitAttribute>().SelectMany(attribute => attribute.GetTraits());

    private static bool HasCategory(IEnumerable<KeyValuePair<string, string>> traits, string value)
        => traits.Any(trait => trait.Key == "Category" && trait.Value == value);
}
