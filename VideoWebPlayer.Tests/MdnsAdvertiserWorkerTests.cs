using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VideoWebPlayer.Configuration;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class MdnsAdvertiserWorkerTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void IsAdvertisementEnabled_RequiresBothSwitches(bool configurationEnabled, bool adminSwitchEnabled, bool expected)
    {
        Assert.Equal(expected, MdnsAdvertiserWorker.IsAdvertisementEnabled(configurationEnabled, adminSwitchEnabled));
    }

    [Fact]
    public async Task ExecuteAsync_ConfigDisabled_ExitsWithoutPollingAdminSwitch()
    {
        // IOptions<MdnsOptions> ist statisch: bei Mdns:Enabled=false kann die Konjunktion
        // nie wahr werden — der Worker darf nicht in die 60-s-DB-Polling-Schleife laufen.
        var worker = CreateWorker(
            enabled: false,
            scopeFactory: new Mock<IServiceScopeFactory>(MockBehavior.Strict),
            featuresAccessed: null,
            out var scopeFactory,
            out var server);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(worker.ExecuteTask.IsCompletedSuccessfully);
        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
        server.VerifyGet(s => s.Features, Times.Never);
        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_AdminSwitchInitialReadFails_DoesNotAdvertise()
    {
        // Fail-closed: schlägt die erste DB-Lesung des Admin-Schalters fehl, darf nicht
        // advertised werden — der Schalter könnte deaktiviert sein, ohne dass sein Wert
        // je gelesen wurde.
        var readAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var featuresAccessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(p => p.GetService(typeof(ProgramSettingsService)))
            .Callback(() => readAttempted.TrySetResult())
            .Returns(null!);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scope.Object);

        var worker = CreateWorker(
            enabled: true,
            scopeFactory: scopeFactoryMock,
            featuresAccessed,
            out _,
            out var server);

        await worker.StartAsync(TestContext.Current.CancellationToken);

        // Abwarten, bis die erste DB-Lesung tatsächlich versucht wurde — die danach
        // folgende Advertise-Entscheidung läuft ohne weiteres Await durch.
        await readAttempted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Ein Advertise-Versuch würde IServer.Features lesen (wirft im Mock und
        // signalisiert den Zugriff). Innerhalb des Fensters muss das ausbleiben.
        await Assert.ThrowsAsync<TimeoutException>(
            () => featuresAccessed.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        server.VerifyGet(s => s.Features, Times.Never);

        await worker.StopAsync(TestContext.Current.CancellationToken);
    }

    private static MdnsAdvertiserWorker CreateWorker(
        bool enabled,
        Mock<IServiceScopeFactory> scopeFactory,
        TaskCompletionSource? featuresAccessed,
        out Mock<IServiceScopeFactory> outScopeFactory,
        out Mock<IServer> outServer)
    {
        var options = Options.Create(new MdnsOptions { Enabled = enabled });
        var configuration = new ConfigurationBuilder().Build();

        var lifetime = new Mock<IHostApplicationLifetime>();
        lifetime.SetupGet(l => l.ApplicationStarted).Returns(new CancellationToken(canceled: true));

        var server = new Mock<IServer>();
        if (featuresAccessed is not null)
        {
            // Zugriff auf IServer.Features markiert den Beginn eines Advertise-Versuchs;
            // das Werfen verhindert zusätzlich, dass TryAdvertise eine echte
            // ServiceDiscovery-Instanz (UDP-Multicast) erzeugt.
            server.SetupGet(s => s.Features)
                .Callback(() => featuresAccessed.TrySetResult())
                .Throws(new InvalidOperationException("Keine Server-Features im Test."));
        }

        outScopeFactory = scopeFactory;
        outServer = server;
        return new MdnsAdvertiserWorker(
            options,
            configuration,
            server.Object,
            lifetime.Object,
            scopeFactory.Object,
            NullLogger<MdnsAdvertiserWorker>.Instance);
    }
}
