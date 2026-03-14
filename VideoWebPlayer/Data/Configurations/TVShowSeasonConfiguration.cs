using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="TVShowSeason"/>.
    /// </summary>
    public sealed class TVShowSeasonConfiguration : IEntityTypeConfiguration<TVShowSeason>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TVShowSeason> builder)
        {
            builder
                .HasOne(s => s.TVShow)
                .WithMany(t => t.Seasons)
                .HasForeignKey(s => s.TVShowId);
        }
    }
}
