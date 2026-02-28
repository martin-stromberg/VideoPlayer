using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services.Authentication;

namespace VideoWebPlayer.Tests.Helpers;

public sealed class TestAuthService : IAuthService
{
    public ApplicationUser? CurrentUser => null;

    public Task<AuthorizationToken> ImpersonateAsync(ImpersonateRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<AuthorizationToken> LoginAsync(AuthenticationRequest request)
    {
        throw new NotImplementedException();
    }
}
