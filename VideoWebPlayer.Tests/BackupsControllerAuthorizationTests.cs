using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VideoWebPlayer.Controllers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class BackupsControllerAuthorizationTests
{
    [Fact]
    public void BackupsController_RequiresAdminOnlyPolicy()
    {
        var authorize = typeof(BackupsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .Single();

        Assert.Equal("AdminOnly", authorize.Policy);
    }

    [Fact]
    public void DownloadEndpoint_IsOnlyExposedThroughProtectedController()
    {
        var method = typeof(BackupsController).GetMethod(nameof(BackupsController.Download));

        Assert.NotNull(method);
        var httpGet = method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true)
            .OfType<HttpGetAttribute>()
            .Single();
        Assert.Equal("download/{fileName}", httpGet.Template);
    }

    [Fact]
    public void CreateEndpoint_UsesServerSidePostWithAntiforgeryValidation()
    {
        var method = typeof(BackupsController).GetMethod(nameof(BackupsController.Create));

        Assert.NotNull(method);
        var httpPost = method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
            .OfType<HttpPostAttribute>()
            .Single();
        Assert.Equal("create", httpPost.Template);
        Assert.Contains(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true), x => x is ValidateAntiForgeryTokenAttribute);
    }
}
