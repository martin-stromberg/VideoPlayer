using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="TVShowEpisodeMediaItem"/>.
    /// </summary>
    public sealed class TVShowEpisodeMediaItemConfiguration : IEntityTypeConfiguration<TVShowEpisodeMediaItem>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TVShowEpisodeMediaItem> builder)
        {
            builder.HasKey(x => new { x.TVShowEpisodeId, x.MediaItemId });

            builder
                .HasOne(x => x.TVShowEpisode)
                .WithMany()
                .HasForeignKey(x => x.TVShowEpisodeId);

            builder
                .HasOne(x => x.MediaItem)
                .WithMany()
                .HasForeignKey(x => x.MediaItemId);
        }
    }
}
