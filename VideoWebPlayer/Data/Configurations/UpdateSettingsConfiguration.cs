using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations;

/// <summary>
/// Configures update settings persistence.
/// </summary>
public sealed class UpdateSettingsConfiguration : IEntityTypeConfiguration<UpdateSettings>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UpdateSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceName).HasMaxLength(200);
        builder.Property(x => x.UpdateBackupPath).HasMaxLength(1024).IsRequired();
    }
}
