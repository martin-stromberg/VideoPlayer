namespace VideoWebPlayer.Tests.Infrastructure.Testing;

/// <summary>Zentrale Namen fuer xUnit-Testkategorien.</summary>
public static class TestCategories
{
    /// <summary>Browser-/Server-End-to-End-Tests (fachlicher Marker; die Lane-Zugehoerigkeit steuert <see cref="OsInterface"/>).</summary>
    public const string E2E = "E2E";

    /// <summary>Tests mit echter Betriebssystem-Schnittstellen-Beruehrung.</summary>
    public const string OsInterface = "OsInterface";

    /// <summary>Nachweislich umgebungsflaky OS-Schnittstellen-Tests (nur in Kombination mit <see cref="OsInterface"/>).</summary>
    public const string Flaky = "Flaky";
}
