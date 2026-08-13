using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="TVShowEpisode"/>.
    /// </summary>
    public sealed class TVShowEpisodeConfiguration : IEntityTypeConfiguration<TVShowEpisode>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TVShowEpisode> builder)
        {
            builder
                .HasOne(e => e.TVShowSeason)
                .WithMany(s => s.Episodes)
                .HasForeignKey(e => e.TVShowSeasonId);

            builder
                .HasOne(e => e.GeneratedBackgroundPicture)
                .WithMany()
                .HasForeignKey(e => e.GeneratedBackgroundPictureId);
        }
    }
}
