using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the program-schedule content scope ("Inhaltsspektrum") of
/// <see cref="ProgramSettingsService"/>: the restrictive default, the update/get roundtrip,
/// the read-side normalization of corrupted values and the enum validation.
/// </summary>
public sealed class ProgramSettingsServiceContentScopeTests : IAsyncLifetime
{
    private ProgramScheduleTestDb _fixture = null!;
    private ProgramSettingsService _service = null!;

    public async ValueTask InitializeAsync()
    {
        _fixture = await ProgramScheduleTestDb.CreateAsync($"scope-settings-{Guid.NewGuid():N}", TestContext.Current.CancellationToken);
        _service = new ProgramSettingsService(_fixture.Db, NullLogger<ProgramSettingsService>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        _fixture.Dispose();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetProgramScheduleContentScope_DefaultsToSharedOnly()
    {
        Assert.Equal(
            ProgramScheduleContentScope.SharedOnly,
            await _service.GetProgramScheduleContentScopeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetProgramScheduleContentScope_InvalidStoredValue_NormalizesToSharedOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var setup = await _fixture.Db.Setups.FirstAsync(ct);
        setup.ProgramScheduleContentScope = (ProgramScheduleContentScope)5;
        await _fixture.Db.SaveChangesAsync(ct);

        Assert.Equal(ProgramScheduleContentScope.SharedOnly, await _service.GetProgramScheduleContentScopeAsync(ct));
    }

    [Fact]
    public async Task UpdateProgramScheduleContentScope_Roundtrip()
    {
        var ct = TestContext.Current.CancellationToken;

        await _service.UpdateProgramScheduleContentScopeAsync(ProgramScheduleContentScope.AllContent, ct);

        Assert.Equal(ProgramScheduleContentScope.AllContent, await _service.GetProgramScheduleContentScopeAsync(ct));
        Assert.Equal(ProgramScheduleContentScope.AllContent, (await _fixture.Db.Setups.FirstAsync(ct)).ProgramScheduleContentScope);

        await _service.UpdateProgramScheduleContentScopeAsync(ProgramScheduleContentScope.SharedOnly, ct);

        Assert.Equal(ProgramScheduleContentScope.SharedOnly, await _service.GetProgramScheduleContentScopeAsync(ct));
        Assert.Equal(ProgramScheduleContentScope.SharedOnly, (await _fixture.Db.Setups.FirstAsync(ct)).ProgramScheduleContentScope);
    }

    [Fact]
    public async Task UpdateProgramScheduleContentScope_InvalidValue_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateProgramScheduleContentScopeAsync(
                (ProgramScheduleContentScope)5, TestContext.Current.CancellationToken));
    }
}
