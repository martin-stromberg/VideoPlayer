using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// N28: per-user release of the TV channel on top of the global
/// <see cref="Setup.TvSenderEnabled"/> flag — <see cref="ProgramSettingsService.IsTvSenderAccessGrantedAsync"/>
/// requires flag AND (admin OR allowlist row); the allowlist replace-update stays consistent
/// against real SQLite with the AspNetUsers foreign key enforced.
/// </summary>
public sealed class TvSenderUserAccessTests
{
    private static ProgramSettingsService Service(ProgramScheduleTestDb fixture)
        => new(fixture.Db, NullLogger<ProgramSettingsService>.Instance);

    [Fact]
    public async Task IsGranted_RequiresFlagAndAllowlistOrAdmin()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-access", ct);
        var service = Service(fixture);
        var user = await fixture.AddUserAsync("user@test.com");
        var admin = await fixture.AddUserAsync("admin@test.com", isAdmin: true);

        // Flag an (Fixture-Default), keine Zeilen: nur Admin implizit.
        Assert.False(await service.IsTvSenderAccessGrantedAsync(user.Id, isAdmin: false, ct));
        Assert.True(await service.IsTvSenderAccessGrantedAsync(admin.Id, isAdmin: true, ct));

        // Freigabe für den Benutzer.
        await service.UpdateTvSenderAllowedUserIdsAsync([user.Id], ct);
        Assert.True(await service.IsTvSenderAccessGrantedAsync(user.Id, isAdmin: false, ct));

        // Flag aus: selbst Freigabe reicht nicht.
        await service.UpdateTvSenderEnabledAsync(false, ct);
        Assert.False(await service.IsTvSenderAccessGrantedAsync(user.Id, isAdmin: false, ct));
        Assert.False(await service.IsTvSenderAccessGrantedAsync(admin.Id, isAdmin: true, ct));
        await service.UpdateTvSenderEnabledAsync(true, ct);

        // Entzug: Replace-Semantik entfernt die Zeile.
        await service.UpdateTvSenderAllowedUserIdsAsync([], ct);
        Assert.False(await service.IsTvSenderAccessGrantedAsync(user.Id, isAdmin: false, ct));
        Assert.Empty(await service.GetTvSenderAllowedUserIdsAsync(ct));

        // Unbekannte/leere Id: verweigert.
        Assert.False(await service.IsTvSenderAccessGrantedAsync(null, isAdmin: false, ct));
        Assert.False(await service.IsTvSenderAccessGrantedAsync("ghost", isAdmin: false, ct));
    }

    [Fact]
    public async Task UpdateAllowedUserIds_ReplacesAtomically()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-access-update", ct);
        var service = Service(fixture);
        var a = await fixture.AddUserAsync("a@test.com");
        var b = await fixture.AddUserAsync("b@test.com");

        await service.UpdateTvSenderAllowedUserIdsAsync([a.Id, b.Id], ct);
        Assert.Equal(new HashSet<string> { a.Id, b.Id }, await service.GetTvSenderAllowedUserIdsAsync(ct));

        // b entfernt, a bleibt.
        await service.UpdateTvSenderAllowedUserIdsAsync([a.Id], ct);
        Assert.Equal(new HashSet<string> { a.Id }, await service.GetTvSenderAllowedUserIdsAsync(ct));
        Assert.Single(fixture.Db.TvSenderUserAccess.Where(x => x.UserId == a.Id));

        // Leere/Whitespace-Ids werden ignoriert.
        await service.UpdateTvSenderAllowedUserIdsAsync([a.Id, "", " "], ct);
        Assert.Equal(new HashSet<string> { a.Id }, await service.GetTvSenderAllowedUserIdsAsync(ct));
    }

    [Fact]
    public async Task AccessRow_RequiresExistingUser_FkEnforced()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("tvsender-access-fk", ct);

        fixture.Db.TvSenderUserAccess.Add(new TvSenderUserAccess { UserId = "does-not-exist" });
        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.Db.SaveChangesAsync(ct));
    }
}
