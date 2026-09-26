using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests which configured API tokens <see cref="global::ApiTokenCheckAttribute"/> accepts per
/// <see cref="global::ApiTokenScope"/>: <c>AnyClient</c> accepts the legacy, web and MAUI tokens, <c>MauiOnly</c>
/// accepts only the MAUI token.
/// </summary>
public class ApiTokenScopeTests
{
    private const string LegacyToken = "legacy-api-token";
    private const string WebToken = "web-api-token";
    private const string MauiToken = "maui-api-token";

    /// <summary>
    /// Every configured token is accepted for the <c>AnyClient</c> scope.
    /// </summary>
    /// <param name="token">The token sent in the <c>X-API-Key</c> header.</param>
    [Theory]
    [InlineData(LegacyToken)]
    [InlineData(WebToken)]
    [InlineData(MauiToken)]
    public async Task AnyClient_AcceptsTheLegacyWebAndMauiTokens(string token)
    {
        var context = await ExecuteAsync(global::ApiTokenScope.AnyClient, token);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task AnyClient_RejectsAnUnknownToken()
    {
        var context = await ExecuteAsync(global::ApiTokenScope.AnyClient, "unknown-token");

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    /// <summary>
    /// The <c>MauiOnly</c> scope is stricter: only the MAUI token passes.
    /// </summary>
    /// <param name="token">The token sent in the <c>X-API-Key</c> header.</param>
    /// <param name="accepted">Whether the token must be accepted.</param>
    [Theory]
    [InlineData(LegacyToken, false)]
    [InlineData(WebToken, false)]
    [InlineData(MauiToken, true)]
    public async Task MauiOnly_AcceptsOnlyTheMauiToken(string token, bool accepted)
    {
        var context = await ExecuteAsync(global::ApiTokenScope.MauiOnly, token);

        Assert.Equal(accepted, context.Result is null);
    }

    [Fact]
    public async Task DefaultScope_IsAnyClient()
    {
        var context = await ExecuteAsync(scope: null, LegacyToken);

        Assert.Null(context.Result);
    }

    private static async Task<ActionExecutingContext> ExecuteAsync(global::ApiTokenScope? scope, string token)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ApiToken"] = LegacyToken,
                ["Jwt:ApiToken:Web"] = WebToken,
                ["Jwt:ApiToken:Maui"] = MauiToken
            })
            .Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.Headers["X-API-Key"] = token;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        var attribute = scope is { } s ? new global::ApiTokenCheckAttribute(s) : new global::ApiTokenCheckAttribute();
        await attribute.OnActionExecutionAsync(context, () => Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object())));
        return context;
    }
}
