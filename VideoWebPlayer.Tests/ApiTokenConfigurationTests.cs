using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Extensions;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class ApiTokenConfigurationTests
{
    [Fact]
    public void AddVideoWebPlayerServices_ProductionRequiresMauiApiToken()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Convert.ToBase64String(new byte[32]),
            ["Jwt:ApiToken"] = "legacy-api-token",
            ["Jwt:ApiToken:Web"] = "web-api-token",
            ["Jwt:ApiToken:Maui"] = ""
        });

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddVideoWebPlayerServices());

        Assert.Contains("Jwt:ApiToken:Maui", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue()
    {
        const string invalidToken = "leaked-production-api-token";
        var messages = new ConcurrentQueue<string>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ApiToken:Maui"] = "valid-maui-token"
            })
            .Build();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<ILogger<global::ApiTokenCheckAttribute>>(new ListLogger<global::ApiTokenCheckAttribute>(messages))
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        httpContext.Request.Headers["X-API-Key"] = invalidToken;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());

        new global::ApiTokenCheckAttribute(global::ApiTokenScope.MauiOnly).OnActionExecuting(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
        var warning = Assert.Single(messages);
        Assert.Contains("Invalid API token", warning, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidToken, warning, StringComparison.Ordinal);
    }
}
