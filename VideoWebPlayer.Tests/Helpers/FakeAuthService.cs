using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Minimal <see cref="IAuthService"/> test double that simply returns a preset <see cref="CurrentUser"/>.
/// </summary>
public sealed class FakeAuthService : IAuthService
{
    public ApplicationUser? CurrentUser { get; set; }

    public Task<AuthorizationToken> ImpersonateAsync(ImpersonateRequest request) => Task.FromResult(new AuthorizationToken());

    public Task<AuthorizationToken> LoginAsync(AuthenticationRequest request) => Task.FromResult(new AuthorizationToken());
}
