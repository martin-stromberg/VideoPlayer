using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests for <see cref="InternalMediaStreamTokenService"/>: loopback URL building from the
/// server's bound addresses, option handling and token resolution/expiry.
/// </summary>
public class InternalMediaStreamTokenServiceTests
{
    private static readonly Regex UriPattern = new(
        @"^http://(?<host>[^:]+):(?<port>\d+)/api/internal/media-stream/(?<token>[0-9a-f]{64})$",
        RegexOptions.Compiled);

    private static FakeServer CreateServer(params string[] addresses)
    {
        var server = new FakeServer();
        var feature = new ServerAddressesFeature();
        foreach (var address in addresses)
            feature.Addresses.Add(address);
        server.Features.Set<IServerAddressesFeature>(feature);
        return server;
    }

    private static InternalMediaStreamTokenService CreateService(
        IServer? server,
        MediaProbeOptions? options = null,
        TimeProvider? timeProvider = null)
        => new(
            server,
            Options.Create(options ?? new MediaProbeOptions()),
            NullLogger<InternalMediaStreamTokenService>.Instance,
            timeProvider);

    [Fact]
    public void TryCreateProbeUri_WildcardAddress_ReturnsLoopbackUriWithResolvableToken()
    {
        var service = CreateService(CreateServer("http://*:5002"));

        var uri = service.TryCreateProbeUri(42);

        var match = UriPattern.Match(uri ?? string.Empty);
        Assert.True(match.Success, $"unexpected probe URI '{uri}'");
        Assert.Equal("127.0.0.1", match.Groups["host"].Value);
        Assert.Equal("5002", match.Groups["port"].Value);
        Assert.Equal(42, service.ResolveMediaItemId(match.Groups["token"].Value));
    }

    [Theory]
    [InlineData("http://+:5002", "http://127.0.0.1:5002")]
    [InlineData("http://0.0.0.0:8080", "http://127.0.0.1:8080")]
    [InlineData("http://[::]:8080", "http://127.0.0.1:8080")]
    [InlineData("http://localhost:5039", "http://127.0.0.1:5039")]
    [InlineData("http://[::1]:5002", "http://127.0.0.1:5002")]
    [InlineData("http://127.0.0.1:7000", "http://127.0.0.1:7000")]
    [InlineData("http://192.168.1.20:5002", "http://192.168.1.20:5002")]
    public void TryNormalizeServerAddress_NormalizesHosts(string address, string expectedBase)
    {
        Assert.Equal(expectedBase, InternalMediaStreamTokenService.TryNormalizeServerAddress(address));
    }

    [Theory]
    [InlineData("https://*:5002")]
    [InlineData("ftp://host:21")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void TryNormalizeServerAddress_NonHttpOrInvalid_ReturnsNull(string address)
    {
        Assert.Null(InternalMediaStreamTokenService.TryNormalizeServerAddress(address));
    }

    [Fact]
    public void TryCreateProbeUri_OnlyHttpsAddress_ReturnsNull()
    {
        var service = CreateService(CreateServer("https://*:5002"));

        Assert.Null(service.TryCreateProbeUri(42));
    }

    [Fact]
    public void TryCreateProbeUri_Disabled_ReturnsNull()
    {
        var service = CreateService(
            CreateServer("http://*:5002"),
            new MediaProbeOptions { HttpProbeEnabled = false });

        Assert.Null(service.TryCreateProbeUri(42));
    }

    [Fact]
    public void TryCreateProbeUri_ConfiguredBaseUrl_WinsOverAutoDetection()
    {
        var service = CreateService(
            CreateServer("http://*:5002"),
            new MediaProbeOptions { HttpProbeBaseUrl = "http://127.0.0.1:9999/" });

        var uri = service.TryCreateProbeUri(42);

        Assert.StartsWith("http://127.0.0.1:9999/api/internal/media-stream/", uri);
    }

    [Fact]
    public void TryCreateProbeUri_InvalidConfiguredBaseUrl_FallsBackToAutoDetection()
    {
        var service = CreateService(
            CreateServer("http://*:5002"),
            new MediaProbeOptions { HttpProbeBaseUrl = "gurke" });

        var uri = service.TryCreateProbeUri(42);

        Assert.StartsWith("http://127.0.0.1:5002/api/internal/media-stream/", uri);
    }

    [Fact]
    public void ResolveMediaItemId_UnknownToken_ReturnsNull()
    {
        var service = CreateService(CreateServer("http://*:5002"));

        Assert.Null(service.ResolveMediaItemId("unknown-token"));
        Assert.Null(service.ResolveMediaItemId(string.Empty));
    }

    [Fact]
    public void ResolveMediaItemId_ExpiredToken_ReturnsNull()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateService(
            CreateServer("http://*:5002"),
            new MediaProbeOptions { HttpProbeTokenLifetimeSeconds = 60 },
            timeProvider);

        var uri = service.TryCreateProbeUri(42);
        var token = UriPattern.Match(uri!).Groups["token"].Value;

        timeProvider.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(42, service.ResolveMediaItemId(token));

        timeProvider.Advance(TimeSpan.FromSeconds(2));
        Assert.Null(service.ResolveMediaItemId(token));
    }

    [Fact]
    public void TryCreateProbeUri_NoServer_ReturnsNull()
    {
        var service = CreateService(server: null);

        Assert.Null(service.TryCreateProbeUri(42));
    }

    /// <summary>
    /// Minimal <see cref="IServer"/> fake exposing a settable feature collection — the real
    /// server is irrelevant; only <see cref="IServerAddressesFeature"/> is consumed.
    /// </summary>
    private sealed class FakeServer : IServer
    {
        public IFeatureCollection Features { get; } = new FeatureCollection();

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose() { }
    }
}
