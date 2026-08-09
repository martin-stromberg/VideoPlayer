using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
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
    public void CreateEndpoint_IsExposedAsServerSidePost()
    {
        var method = typeof(BackupsController).GetMethod(nameof(BackupsController.Create));

        Assert.NotNull(method);
        var httpPost = method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
            .OfType<HttpPostAttribute>()
            .Single();
        Assert.Equal("create", httpPost.Template);
    }

    [Fact]
    public void UploadEndpoint_IsExposedAsUnlimitedServerSidePost()
    {
        var method = typeof(BackupsController).GetMethod(nameof(BackupsController.Upload));

        Assert.NotNull(method);
        var httpPost = method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true)
            .OfType<HttpPostAttribute>()
            .Single();
        Assert.Equal("upload", httpPost.Template);

        Assert.NotEmpty(method.GetCustomAttributes(typeof(DisableRequestSizeLimitAttribute), inherit: true));
        var formLimits = method.GetCustomAttributes(typeof(RequestFormLimitsAttribute), inherit: true)
            .OfType<RequestFormLimitsAttribute>()
            .Single();
        Assert.Equal(long.MaxValue, formLimits.MultipartBodyLengthLimit);
    }
}
