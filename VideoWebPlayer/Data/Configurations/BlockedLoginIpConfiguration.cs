using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="BlockedLoginIp"/>.
    /// </summary>
    public sealed class BlockedLoginIpConfiguration : IEntityTypeConfiguration<BlockedLoginIp>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<BlockedLoginIp> builder)
        {
            builder.HasKey(x => x.Ip);
            builder.Property(x => x.Ip).HasMaxLength(64);
            builder.HasIndex(x => x.BlockedAtUtc);
        }
    }
}
