using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
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
    public async Task CreateBackupAsync_DeletesBackupsExceedingRetentionCount()
    {
        var backupDirectory = Path.Combine(_contentRoot, "Backups");
        Directory.CreateDirectory(backupDirectory);
        for (var index = 0; index < 4; index++)
        {
            var existing = WriteBackupFile(backupDirectory, $"old-{index}.zip");
            File.SetLastWriteTimeUtc(existing, DateTime.UtcNow.AddDays(-10 + index));
        }

        var backupService = CreateBackupService(request =>
            UpdateBackupResult.Success(WriteBackupFile(request.TargetDirectory, "new.zip")));

        var coordinator = CreateCoordinator(new UpdateBackupOptions { RetainedBackupCount = 2 }, backupService);

        var mayProceed = await coordinator.CreateBackupAsync("test");

        Assert.True(mayProceed);
        var remaining = Directory.GetFiles(backupDirectory).Select(Path.GetFileName).ToArray();
        Assert.Equal(2, remaining.Length);
        Assert.Contains("new.zip", remaining);
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

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(env => env.ContentRootPath).Returns(_contentRoot);

        return new UpdateBackupCoordinator(
            services.BuildServiceProvider(),
            Options.Create(options),
            environment.Object,
            NullLogger<UpdateBackupCoordinator>.Instance);
    }
}
