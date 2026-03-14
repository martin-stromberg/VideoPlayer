using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="MediaItem"/>.
    /// </summary>
    public sealed class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<MediaItem> builder)
        {
            builder
                .HasOne(mi => mi.MediaCollection)
                .WithMany(mc => mc.MediaItems)
                .HasForeignKey(mi => mi.MediaCollectionId);
        }
    }
}
