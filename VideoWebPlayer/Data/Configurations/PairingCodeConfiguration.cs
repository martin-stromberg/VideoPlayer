using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="PairingCode"/>.
    /// </summary>
    public sealed class PairingCodeConfiguration : IEntityTypeConfiguration<PairingCode>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<PairingCode> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CodeHash).HasMaxLength(128);
            builder.Property(x => x.CreatedByUserId).HasMaxLength(64);
            builder.HasIndex(x => x.CodeHash);
            builder.HasIndex(x => x.ExpiresAtUtc);
        }
    }
}
