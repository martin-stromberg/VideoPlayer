using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations;

/// <summary>
/// Configures backup operation history persistence.
/// </summary>
public sealed class BackupOperationHistoryConfiguration : IEntityTypeConfiguration<BackupOperationHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BackupOperationHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Operation).HasMaxLength(64);
        builder.Property(x => x.FileName).HasMaxLength(512);
        builder.Property(x => x.Generation).HasMaxLength(32);
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.Message).HasMaxLength(2048);
    }
}
