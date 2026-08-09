using msTools.Backup;
using Xunit;

namespace msTools.Backup.Tests;

public sealed class BackupRetentionServiceTests
{
    [Fact]
    public async Task ApplyAsync_DeletesOnlyExpiredAutomaticGenerations()
    {
        var store = new RecordingStore();
        var descriptors = new List<BackupDescriptor>
        {
            Descriptor("manual.zip", BackupGeneration.Manual, 10),
            Descriptor("uploaded.zip", BackupGeneration.Uploaded, 9),
            Descriptor("son-new.zip", BackupGeneration.Son, 8),
            Descriptor("son-old.zip", BackupGeneration.Son, 7),
            Descriptor("father-new.zip", BackupGeneration.Father, 6),
            Descriptor("father-old.zip", BackupGeneration.Father, 5),
            Descriptor("grandfather-new.zip", BackupGeneration.Grandfather, 4),
            Descriptor("grandfather-old.zip", BackupGeneration.Grandfather, 3)
        };

        var service = new BackupRetentionService(store);
        await service.ApplyAsync(descriptors, new BackupRetentionOptions
        {
            SonCount = 1,
            FatherCount = 1,
            GrandfatherCount = 1
        }, CancellationToken.None);

        Assert.Equal(new[] { "son-old.zip", "father-old.zip", "grandfather-old.zip" }, store.Deleted);
    }

    private static BackupDescriptor Descriptor(string fileName, BackupGeneration generation, int days)
        => new(
            fileName,
            fileName,
            1,
            DateTimeOffset.UtcNow.AddDays(days),
            generation,
            "Provider",
            1,
            true,
            Array.Empty<string>());

    private sealed class RecordingStore : IBackupStore
    {
        public List<string> Deleted { get; } = new();

        public Task<IReadOnlyList<BackupDescriptor>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BackupDescriptor> SaveBackupAsync(BackupCreateRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BackupValidationResult> ValidateAsync(Stream source, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<BackupDescriptor> ImportUploadedBackupAsync(Stream source, string originalFileName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(string fileName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(string fileName, CancellationToken cancellationToken)
        {
            Deleted.Add(fileName);
            return Task.CompletedTask;
        }
    }
}
