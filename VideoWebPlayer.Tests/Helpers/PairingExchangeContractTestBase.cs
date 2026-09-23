using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Base class for <c>POST /api/pairing/exchange</c> contract tests against a
/// <see cref="WebApplicationFactory{TEntryPoint}"/> hosted <see cref="global::Program"/>.
/// </summary>
public abstract class PairingExchangeContractTestBase : IDisposable
{
    private readonly string _dbPath;

    protected PairingExchangeContractTestBase()
    {
        _dbPath = PairingWebApplicationFactory.CreateTempDbPath("vwp-pairing-contract");
        Factory = PairingWebApplicationFactory.Create(_dbPath);
    }

    protected WebApplicationFactory<global::Program> Factory { get; }

    protected async Task<string> CreatePairingCodeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var pairingService = scope.ServiceProvider.GetRequiredService<IPairingService>();
        return (await pairingService.CreatePairingCodeAsync(null, TestContext.Current.CancellationToken)).Code;
    }

    protected async Task<string> CreateUserAsync(string password)
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = $"pairing-contract-{Guid.NewGuid():N}",
            Email = $"pairing-contract-{Guid.NewGuid():N}@example.com",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
        return user.Email!;
    }

    protected async Task<HttpContext> SendExchangeAsync(string rawJson, IPAddress? remoteIp = null)
    {
        var body = Encoding.UTF8.GetBytes(rawJson);
        return await Factory.Server.SendAsync(ctx =>
        {
            ctx.Request.Method = HttpMethods.Post;
            ctx.Request.Scheme = "http";
            ctx.Request.Host = new HostString("localhost");
            ctx.Request.Path = "/api/pairing/exchange";
            ctx.Request.ContentType = "application/json";
            ctx.Request.Body = new MemoryStream(body);
            ctx.Request.ContentLength = body.Length;
            if (remoteIp is not null)
                ctx.Connection.RemoteIpAddress = remoteIp;
        }, TestContext.Current.CancellationToken);
    }

    protected static async Task<string> ReadBodyAsync(HttpContext ctx)
        => await new StreamReader(ctx.Response.Body).ReadToEndAsync();

    protected void UnblockIp(string ip)
        => Factory.Services.GetRequiredService<ILoginIpBlockService>().Unblock(ip);

    public void Dispose()
    {
        Factory.Dispose();
        try { File.Delete(_dbPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* temp file may still be locked */ }
    }
}
