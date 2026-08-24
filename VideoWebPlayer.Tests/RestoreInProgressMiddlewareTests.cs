using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using msTools.Backup;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Backups;
using Xunit;

namespace VideoWebPlayer.Tests;

public sealed class RestoreInProgressMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsStatusJsonForApiRequestsDuringRestore()
    {
        var restoreJobs = CreateActiveRestoreJob(out var release);
        var nextCalled = false;
        var middleware = new RestoreInProgressMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/items";
        context.Response.Body = new MemoryStream();

        try
        {
            await WaitForActiveRestoreAsync(restoreJobs);

            await middleware.InvokeAsync(context, restoreJobs);

            Assert.False(nextCalled);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
            context.Response.Body.Position = 0;
            var response = await JsonSerializer.DeserializeAsync<RestoreInProgressResponse>(
                context.Response.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                TestContext.Current.CancellationToken);
            Assert.NotNull(response);
            Assert.True(response.RestoreInProgress);
            Assert.Equal("Running", response.Status);
        }
        finally
        {
            release.SetResult();
        }
    }

    [Fact]
    public async Task InvokeAsync_AllowsBackupAdminRoutesDuringRestore()
    {
        var restoreJobs = CreateActiveRestoreJob(out var release);
        var nextCalled = false;
        var middleware = new RestoreInProgressMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/admin/backups";

        try
        {
            await WaitForActiveRestoreAsync(restoreJobs);

            await middleware.InvokeAsync(context, restoreJobs);

            Assert.True(nextCalled);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
        finally
        {
            release.SetResult();
        }
    }

    private static RestoreBackupJobService CreateActiveRestoreJob(out TaskCompletionSource release)
    {
        release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var services = new ServiceCollection();
        services.AddSingleton(new EventManager());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IBackupDataSource, NoopBackupDataSource>();
        services.AddScoped<VideoWebPlayerBackupDataFactory>();
        services.AddScoped<IBackupOptionsProvider, NoopBackupOptionsProvider>();
        services.AddScoped<BackupSettingsService>();
        services.AddScoped<BackupOperationHistoryService>();
        services.AddScoped<VideoWebPlayerBackupFacade>();
        services.AddSingleton<IBackupService>(new BlockingBackupService(release.Task));
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        var service = new RestoreBackupJobService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RestoreBackupJobService>.Instance);
        service.StartRestore("backup.bak", "admin", confirmRestore: true);
        return service;
    }

    private static async Task WaitForActiveRestoreAsync(RestoreBackupJobService service)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (service.GetSnapshot().Status == RestoreBackupJobStatus.Running)
                return;

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("Restore job did not become active.");
    }

    private sealed class BlockingBackupService : IBackupService
    {
        private readonly Task _release;

        public BlockingBackupService(Task release)
        {
            _release = release;
        }

        public Task<IReadOnlyList<BackupDescriptor>> ListBackupsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<BackupDescriptor>>(Array.Empty<BackupDescriptor>());

        public Task<Stream> OpenBackupReadAsync(string fileName, CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task<BackupOperationResult> DeleteBackupAsync(string fileName, CancellationToken cancellationToken)
            => Task.FromResult(BackupOperationResult.Success("Backup wurde gelöscht."));

        public Task<BackupResult> StoreAsync(string backupName, BackupGeneration generation, IEnumerable<IBackupData> items, CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupResult(backupName, true, "Backup wurde gespeichert."));

        public async Task<IReadOnlyList<IBackupData>> RestoreAsync(string backupName, IBackupDataFactory factory, CancellationToken cancellationToken = default)
        {
            await _release.WaitAsync(cancellationToken);
            return Array.Empty<IBackupData>();
        }

        public Task ApplyRetentionAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class NoopBackupDataSource : IBackupDataSource
    {
        public Task<IReadOnlyList<IBackupData>> GetBackupDataAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<IBackupData>>(Array.Empty<IBackupData>());
    }

    private sealed class NoopBackupOptionsProvider : IBackupOptionsProvider
    {
        public Task<BackupOptions> GetOptionsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new BackupOptions { StoragePath = Path.Combine("Data", "Backups") });
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "VideoWebPlayer";
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
