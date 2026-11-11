using System.Text.RegularExpressions;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Architectural convention: the individual pass steps
/// (<c>EnsureCoverageAsync</c>, <c>ReconcileAsync</c>, <c>PurgeExpiredRulesAsync</c>,
/// <c>CleanupSelfDisplacedSlotsAsync</c>) may only be invoked directly inside the
/// approved step-unit-test files below. Every test simulating a maintenance pass —
/// especially after mutating rules or material — must go through the single pass
/// entry point (<see cref="VideoWebPlayer.Services.IProgramScheduleService.ExecutePassAsync"/>,
/// e.g. via <c>PrimetimeSeed.StepAsync</c>), because a step called outside the
/// production sequence exercises a path that never runs in production.
/// </summary>
/// <returns>Enforces the convention via a source scan of the test project.</returns>
public sealed class ProgramScheduleConventionTests
{
    /// <summary>Test files whose purpose is testing one pass step's own contract —
    /// the only places allowed to call the steps directly. Adding a file here is a
    /// deliberate decision: it means "this file tests the step itself", not "this is
    /// a shortcut around the pass".</summary>
    /// <returns>The allowed file names.</returns>
    private static readonly HashSet<string> StepTestFiles = new(StringComparer.Ordinal)
    {
        "ProgramClipGroupServiceTests.cs",
        "ProgramScheduleServiceTests_Access.cs",
        "ProgramScheduleServiceTests_AgeWindow.cs",
        "ProgramScheduleServiceTests_Boundary.cs",
        "ProgramScheduleServiceTests_Cleanup.cs",
        "ProgramScheduleServiceTests_ClipFillPeriodBounds.cs",
        "ProgramScheduleServiceTests_ClipGroups.cs",
        "ProgramScheduleServiceTests_ClipSource.cs",
        "ProgramScheduleServiceTests_CollectionOrder.cs",
        "ProgramScheduleServiceTests_Conflicts.cs",
        "ProgramScheduleServiceTests_ContentScope.cs",
        "ProgramScheduleServiceTests_CoverageBoundary.cs",
        "ProgramScheduleServiceTests_CursorSelection.cs",
        "ProgramScheduleServiceTests_Filler.cs",
        "ProgramScheduleServiceTests_Generation.cs",
        "ProgramScheduleServiceTests_Genres.cs",
        "ProgramScheduleServiceTests_Halloween.cs",
        "ProgramScheduleServiceTests_Interleaving.cs",
        "ProgramScheduleServiceTests_Issue315.cs",
        "ProgramScheduleServiceTests_Issue320.cs",
        "ProgramScheduleServiceTests_NewRules.cs",
        "ProgramScheduleServiceTests_PrimetimeAgeWindow.cs",
        "ProgramScheduleServiceTests_PrimetimeGrace.cs",
        "ProgramScheduleServiceTests_PrimetimeRolling.cs",
        "ProgramScheduleServiceTests_PrimetimeTrailer.cs",
        "ProgramScheduleServiceTests_PriorityTakeover.cs",
        "ProgramScheduleServiceTests_Purge.cs",
        "ProgramScheduleServiceTests_Reconcile.cs",
        "ProgramScheduleServiceTests_RingOrder.cs",
        "ProgramScheduleServiceTests_Stability.cs",
        "ProgramScheduleServiceTests_StrandRotation.cs",
        "ProgramScheduleServiceTests_Trailer.cs",
    };

    /// <returns>Matches a direct call of a pass step.</returns>
    private static readonly Regex StepCall =
        new(@"\.(EnsureCoverageAsync|ReconcileAsync|PurgeExpiredRulesAsync|CleanupSelfDisplacedSlotsAsync)\(",
            RegexOptions.Compiled);

    /// <summary>Every direct pass-step call lives inside an approved step test.</summary>
    [Fact]
    public void DirectPassStepCalls_OnlyInApprovedStepTests()
    {
        var testDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(testDir, "*.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (StepTestFiles.Contains(name) || name == nameof(ProgramScheduleConventionTests) + ".cs")
                continue;
            if (StepCall.IsMatch(File.ReadAllText(file)))
                violations.Add(Path.GetRelativePath(testDir, file));
        }
        Assert.True(violations.Count == 0,
            "Direktaufrufe der Sendeplan-Schritte außerhalb der Schritt-Tests:\n" +
            string.Join("\n", violations) +
            "\nSimulationen eines Pflegedurchlaufs nutzen ExecutePassAsync (z. B. PrimetimeSeed.StepAsync).");
    }
}
