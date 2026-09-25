using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VideoWebPlayer.Data;
using VideoWebPlayer.Extensions;
using VideoWebPlayer.Services;
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
    public async Task ApiTokenCheckAttribute_InvalidTokenLogDoesNotIncludeHeaderValue()
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
        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await new global::ApiTokenCheckAttribute(global::ApiTokenScope.MauiOnly).OnActionExecutionAsync(context, Next);

        Assert.IsType<UnauthorizedResult>(context.Result);
        Assert.False(nextCalled);
        var warning = Assert.Single(messages);
        Assert.Contains("Invalid API token", warning, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidToken, warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiTokenCheckAttribute_MauiOnly_AcceptsDeviceToken()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateAttributeFixtureAsync("attr-maui-device-token", ct);
        var deviceToken = await fixture.DeviceTokens.IssueAsync("Test-TV", null, ct);
        var context = CreateContext(fixture.Scope.ServiceProvider, deviceToken, out var actionContext);
        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await new global::ApiTokenCheckAttribute(global::ApiTokenScope.MauiOnly).OnActionExecutionAsync(context, Next);

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task ApiTokenCheckAttribute_MauiOnly_RejectsRevokedDeviceToken()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateAttributeFixtureAsync("attr-maui-revoked", ct);
        var deviceToken = await fixture.DeviceTokens.IssueAsync("Test-TV", null, ct);
        var deviceId = (await fixture.Db.PairedDevices.SingleAsync(ct)).Id;
        await fixture.DeviceTokens.RevokeAsync(deviceId, ct);
        var context = CreateContext(fixture.Scope.ServiceProvider, deviceToken, out var actionContext);
        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await new global::ApiTokenCheckAttribute(global::ApiTokenScope.MauiOnly).OnActionExecutionAsync(context, Next);

        Assert.False(nextCalled);
        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public async Task ApiTokenCheckAttribute_MauiOnly_MauiConfigTokenStillAccepted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateAttributeFixtureAsync("attr-maui-config", ct);
        var context = CreateContext(fixture.Scope.ServiceProvider, "valid-maui-token", out var actionContext);
        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await new global::ApiTokenCheckAttribute(global::ApiTokenScope.MauiOnly).OnActionExecutionAsync(context, Next);

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task ApiTokenCheckAttribute_AnyClient_DoesNotTouchDeviceTokens()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await CreateAttributeFixtureAsync("attr-anyclient", ct);
        var recordingService = new RecordingDeviceTokenService();
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(fixture.Configuration)
            .AddSingleton<IDeviceTokenService>(recordingService)
            .BuildServiceProvider();
        var context = CreateContext(services, "some-device-token", out var actionContext);
        var nextCalled = false;
        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), new object()));
        }

        await new global::ApiTokenCheckAttribute(global::ApiTokenScope.AnyClient).OnActionExecutionAsync(context, Next);

        Assert.False(nextCalled);
        Assert.IsType<UnauthorizedResult>(context.Result);
        Assert.Equal(0, recordingService.IsValidDeviceTokenCallCount);
    }

    private static ActionExecutingContext CreateContext(IServiceProvider services, string token, out ActionContext actionContext)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        httpContext.Request.Headers["X-API-Key"] = token;
        actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static async Task<AttributeFixture> CreateAttributeFixtureAsync(string dbName, CancellationToken ct)
    {
        var connectionString = $"Data Source=file:{dbName}?mode=memory&cache=shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:ApiToken:Maui"] = "valid-maui-token"
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddLogging()
            .AddSingleton<EventManager>()
            .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString))
            .AddScoped<IDeviceTokenService, DeviceTokenService>()
            .BuildServiceProvider();

        var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        return new AttributeFixture(connection, services, scope, configuration, db, scope.ServiceProvider.GetRequiredService<IDeviceTokenService>());
    }

    private sealed class AttributeFixture : IDisposable
    {
        public AttributeFixture(SqliteConnection connection, ServiceProvider services, IServiceScope scope, IConfiguration configuration, ApplicationDbContext db, IDeviceTokenService deviceTokens)
        {
            Connection = connection;
            Services = services;
            Scope = scope;
            Configuration = configuration;
            Db = db;
            DeviceTokens = deviceTokens;
        }

        public SqliteConnection Connection { get; }
        public ServiceProvider Services { get; }
        public IServiceScope Scope { get; }
        public IConfiguration Configuration { get; }
        public ApplicationDbContext Db { get; }
        public IDeviceTokenService DeviceTokens { get; }

        public void Dispose()
        {
            Scope.Dispose();
            Services.Dispose();
            Connection.Dispose();
        }
    }

    private sealed class RecordingDeviceTokenService : IDeviceTokenService
    {
        public int IsValidDeviceTokenCallCount { get; private set; }

        public Task<string> IssueAsync(string? deviceName, string? createdByUserId, CancellationToken cancellationToken = default)
            => Task.FromResult("unused");

        public Task<bool> IsValidDeviceTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            IsValidDeviceTokenCallCount++;
            return Task.FromResult(false);
        }

        public Task<bool> RevokeAsync(int deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> RenameAsync(int deviceId, string newName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<IReadOnlyList<PairedDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PairedDevice>>(new List<PairedDevice>());
    }
}
