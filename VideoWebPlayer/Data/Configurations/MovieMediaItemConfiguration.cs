using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="MovieMediaItem"/>.
    /// </summary>
    public sealed class MovieMediaItemConfiguration : IEntityTypeConfiguration<MovieMediaItem>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<MovieMediaItem> builder)
        {
            builder.HasKey(x => new { x.MovieId, x.MediaItemId });

            builder
                .HasOne(x => x.Movie)
                .WithMany()
                .HasForeignKey(x => x.MovieId);

            builder
                .HasOne(x => x.MediaItem)
                .WithMany()
                .HasForeignKey(x => x.MediaItemId);
        }
    }
}
