using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations;

/// <summary>
/// Configures backup settings persistence.
/// </summary>
public sealed class BackupSettingsConfiguration : IEntityTypeConfiguration<BackupSettings>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BackupSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StoragePath).HasMaxLength(1024);
    }
}
