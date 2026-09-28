using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="PairedDevice"/>.
    /// </summary>
    public sealed class PairedDeviceConfiguration : IEntityTypeConfiguration<PairedDevice>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<PairedDevice> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(200);
            builder.Property(x => x.TokenHash).HasMaxLength(128);
            builder.Property(x => x.CreatedByUserId).HasMaxLength(64);
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => x.RevokedAtUtc);
        }
    }
}
