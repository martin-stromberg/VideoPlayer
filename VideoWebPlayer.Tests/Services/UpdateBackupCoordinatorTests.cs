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
/// Tests für den Backup-Ablauf vor der Installation eines Programmupdates.
/// </summary>
public class UpdateBackupCoordinatorTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"vwp-backup-tests-{Guid.NewGuid():N}");

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
    public async Task CreateBackupAsync_WithoutRegisteredBackupService_BlocksInstallation()
    {
        var coordinator = CreateCoordinator(new UpdateBackupOptions(), backupService: null);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.False(mayProceed);
    }

    [Fact]
    public async Task CreateBackupAsync_WithoutRegisteredBackupServiceAndFailureAllowed_AllowsInstallation()
    {
        var options = new UpdateBackupOptions { CancelInstallationOnFailure = false };
        var coordinator = CreateCoordinator(options, backupService: null);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.True(mayProceed);
    }

    [Fact]
    public async Task CreateBackupAsync_WhenDisabled_SkipsBackupService()
    {
        var backupService = new Mock<IUpdateBackupService>(MockBehavior.Strict);
        var coordinator = CreateCoordinator(new UpdateBackupOptions { Enabled = false }, backupService.Object);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.True(mayProceed);
        backupService.Verify(
            service => service.CreateBackupAsync(It.IsAny<UpdateBackupRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBackupAsync_PassesExistingConfiguredDirectory()
    {
        string? requestedDirectory = null;
        var configuredDirectory = Path.Combine(_contentRoot, "Sicherungen");
        Directory.CreateDirectory(configuredDirectory);
        var backupService = CreateBackupService(request =>
        {
            requestedDirectory = request.TargetDirectory;
            return UpdateBackupResult.Success(WriteBackupFile(request.TargetDirectory, "backup.zip"));
        });

        var coordinator = CreateCoordinator(new UpdateBackupOptions { Path = "Sicherungen" }, backupService);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.True(mayProceed);
        Assert.Equal(Path.Combine(_contentRoot, "Sicherungen"), requestedDirectory);
        Assert.True(Directory.Exists(requestedDirectory));
    }

    [Fact]
    public async Task CreateBackupAsync_DoesNotCreateConfiguredDirectoryBeforeCallingProvider()
    {
        var blockingFile = Path.Combine(_contentRoot, "blocked");
        Directory.CreateDirectory(_contentRoot);
        File.WriteAllText(blockingFile, "not a directory");
        var configuredPath = Path.Combine("blocked", "Updates");
        string? requestedDirectory = null;
        var backupService = CreateBackupService(request =>
        {
            requestedDirectory = request.TargetDirectory;
            return UpdateBackupResult.Success(Path.Combine(_contentRoot, "real-backup.zip"));
        });

        var coordinator = CreateCoordinator(new UpdateBackupOptions { Path = configuredPath }, backupService);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.True(mayProceed);
        Assert.Equal(Path.Combine(_contentRoot, configuredPath), requestedDirectory);
    }

    [Fact]
    public async Task CreateBackupAsync_DoesNotApplyUnsafeRetentionToConfiguredDirectory()
    {
        var backupDirectory = Path.Combine(_contentRoot, "Backups");
        Directory.CreateDirectory(backupDirectory);
        for (var index = 0; index < 4; index++)
        {
            var existing = WriteBackupFile(backupDirectory, $"old-{index}.zip");
            File.SetLastWriteTimeUtc(existing, DateTime.UtcNow.AddDays(-10 + index));
        }

        var infrastructureDirectory = Path.Combine(_contentRoot, "InfrastructureBackups");
        Directory.CreateDirectory(infrastructureDirectory);
        var backupService = CreateBackupService(_ =>
            UpdateBackupResult.Success(WriteBackupFile(infrastructureDirectory, "program-update.zip")));

        var coordinator = CreateCoordinator(new UpdateBackupOptions { RetainedBackupCount = 2 }, backupService);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.True(mayProceed);
        var remaining = Directory.GetFiles(backupDirectory).Select(Path.GetFileName).ToArray();
        Assert.Equal(4, remaining.Length);
        Assert.Contains("old-0.zip", remaining);
        Assert.Contains("old-1.zip", remaining);
        Assert.Contains("old-2.zip", remaining);
        Assert.Contains("old-3.zip", remaining);
    }

    [Fact]
    public async Task CreateBackupAsync_WhenBackupServiceThrows_BlocksInstallation()
    {
        var backupService = CreateBackupService(_ => throw new IOException("Datenträger voll"));
        var coordinator = CreateCoordinator(new UpdateBackupOptions(), backupService);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.False(mayProceed);
    }

    [Fact]
    public async Task CreateBackupAsync_WhenBackupFails_BlocksInstallation()
    {
        var backupService = CreateBackupService(_ => UpdateBackupResult.Failure("kein Platz"));
        var coordinator = CreateCoordinator(new UpdateBackupOptions(), backupService);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.False(mayProceed);
    }

    private static IUpdateBackupService CreateBackupService(Func<UpdateBackupRequest, UpdateBackupResult> handler)
    {
        var backupService = new Mock<IUpdateBackupService>();
        backupService
            .Setup(service => service.CreateBackupAsync(It.IsAny<UpdateBackupRequest>(), It.IsAny<CancellationToken>()))
            .Returns((UpdateBackupRequest request, CancellationToken _) => Task.FromResult(handler(request)));
        return backupService.Object;
    }

    private static string WriteBackupFile(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "backup");
        return path;
    }

    private UpdateBackupCoordinator CreateCoordinator(UpdateBackupOptions options, IUpdateBackupService? backupService)
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
                .UseInMemoryDatabase($"update-backup-coordinator-{Guid.NewGuid():N}")
                .Options;
            return new ApplicationDbContext(dbOptions, new EventManager());
        });

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(env => env.ContentRootPath).Returns(_contentRoot);

        return new UpdateBackupCoordinator(
            services.BuildServiceProvider(),
            environment.Object,
            NullLogger<UpdateBackupCoordinator>.Instance);
    }
}
