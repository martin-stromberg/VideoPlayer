using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="RefreshToken"/>.
    /// </summary>
    public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.TokenHash).HasMaxLength(128);
            builder.Property(x => x.UserId).HasMaxLength(64);
            builder.Property(x => x.ReplacedByHash).HasMaxLength(128);
            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => x.DeviceId);
            builder.HasIndex(x => x.RevokedAtUtc);
        }
    }
}
