using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="TvSenderUserAccess"/>.
    /// </summary>
    public sealed class TvSenderUserAccessConfiguration : IEntityTypeConfiguration<TvSenderUserAccess>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TvSenderUserAccess> builder)
        {
            builder.HasKey(a => a.UserId);
            builder.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
