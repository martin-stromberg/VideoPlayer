using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using msTools.Updater;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Updates;
using Xunit;

namespace VideoWebPlayer.Tests.Services;

/// <summary>
/// Tests für die Anbindung des Pre-Install-Events des Updaters an die Sicherung.
/// </summary>
public class UpdateBackupEventBinderTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-binder-tests-{Guid.NewGuid():N}");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BeforeInstall_WhenBackupSucceeds_InstallationIsNotCanceled()
    {
        var backupService = new Mock<IUpdateBackupService>();
        backupService
            .Setup(service => service.CreateBackupAsync(It.IsAny<UpdateBackupRequest>(), It.IsAny<CancellationToken>()))
            .Returns((UpdateBackupRequest request, CancellationToken _) =>
            {
                Directory.CreateDirectory(request.TargetDirectory);
                var path = Path.Combine(request.TargetDirectory, "backup.zip");
                File.WriteAllText(path, "backup");
                return Task.FromResult(UpdateBackupResult.Success(path));
            });

        var events = await StartBinderAsync(new UpdateBackupOptions(), backupService.Object);

        var canceled = events.RaiseBeforeInstall(this, new FileInfo(Path.Combine(_contentRoot, "release.zip")));

        Assert.False(canceled);
        backupService.Verify(
            service => service.CreateBackupAsync(It.IsAny<UpdateBackupRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BeforeInstall_WithoutBackupService_CancelsInstallation()
    {
        var events = await StartBinderAsync(new UpdateBackupOptions(), backupService: null);

        var canceled = events.RaiseBeforeInstall(this, new FileInfo(Path.Combine(_contentRoot, "release.zip")));

        Assert.True(canceled);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromBeforeInstall()
    {
        var events = new AutoUpdateEvents();
        var binder = CreateBinder(events, new UpdateBackupOptions(), backupService: null);
        await binder.StartAsync(CancellationToken.None);
        await binder.StopAsync(CancellationToken.None);

        var canceled = events.RaiseBeforeInstall(this, new FileInfo(Path.Combine(_contentRoot, "release.zip")));

        Assert.False(canceled);
    }

    private async Task<AutoUpdateEvents> StartBinderAsync(UpdateBackupOptions options, IUpdateBackupService? backupService)
    {
        var events = new AutoUpdateEvents();
        var binder = CreateBinder(events, options, backupService);
        await binder.StartAsync(CancellationToken.None);
        return events;
    }

    private UpdateBackupEventBinder CreateBinder(
        IAutoUpdateEventAggregator events,
        UpdateBackupOptions options,
        IUpdateBackupService? backupService)
    {
        Directory.CreateDirectory(_contentRoot);

        var services = new ServiceCollection();
        if (backupService is not null)
        {
            services.AddSingleton(backupService);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AutoUpdate:Backup:Enabled"] = options.Enabled.ToString(),
                ["AutoUpdate:Backup:Path"] = options.Path,
                ["AutoUpdate:Backup:RetainedBackupCount"] = options.RetainedBackupCount.ToString(),
                ["AutoUpdate:Backup:CancelInstallationOnFailure"] = options.CancelInstallationOnFailure.ToString()
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(new AutoUpdateOptions());
        services.AddSingleton<VideoWebPlayerUpdateSourceFactory>();
        services.AddScoped<UpdateSettingsService>();
        services.AddScoped<IUpdateSettingsService>(sp => sp.GetRequiredService<UpdateSettingsService>());
        services.AddScoped(_ =>
        {
            var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"update-backup-binder-{Guid.NewGuid():N}")
                .Options;
            return new ApplicationDbContext(dbOptions, new EventManager());
        });

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(env => env.ContentRootPath).Returns(_contentRoot);

        var coordinator = new UpdateBackupCoordinator(
            services.BuildServiceProvider(),
            environment.Object,
            NullLogger<UpdateBackupCoordinator>.Instance);

        return new UpdateBackupEventBinder(events, coordinator, NullLogger<UpdateBackupEventBinder>.Instance);
    }
}
