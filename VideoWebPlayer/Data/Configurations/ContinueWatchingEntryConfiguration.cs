using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ContinueWatchingEntry"/>.
    /// </summary>
    public sealed class ContinueWatchingEntryConfiguration : IEntityTypeConfiguration<ContinueWatchingEntry>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ContinueWatchingEntry> builder)
        {
            builder.HasIndex(x => new { x.UserId, x.MovieId });
            builder.HasIndex(x => new { x.UserId, x.TVShowEpisodeId });
            builder.HasIndex(x => new { x.UserId, x.ListOrder, x.UpdatedAt });

            builder
                .HasOne(x => x.Movie)
                .WithMany()
                .HasForeignKey(x => x.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.TVShowEpisode)
                .WithMany()
                .HasForeignKey(x => x.TVShowEpisodeId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
