using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="MediaCollection"/>.
    /// </summary>
    public sealed class MediaCollectionConfiguration : IEntityTypeConfiguration<MediaCollection>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<MediaCollection> builder)
        {
            builder
                .HasOne(mc => mc.MediaSource)
                .WithMany(ms => ms.MediaCollections)
                .HasForeignKey(mc => mc.MediaSourceId);

            builder
                .HasOne(mc => mc.ParentMediaCollection)
                .WithMany(mc => mc.ChildCollections)
                .HasForeignKey(mc => mc.ParentMediaCollectionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
