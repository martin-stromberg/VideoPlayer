using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="TVShowGenre"/>.
    /// </summary>
    public sealed class TVShowGenreConfiguration : IEntityTypeConfiguration<TVShowGenre>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TVShowGenre> builder)
        {
            builder.HasKey(tg => new { tg.TVShowId, tg.GenreId });

            builder
                .HasOne(tg => tg.TVShow)
                .WithMany(t => t.TVShowGenres)
                .HasForeignKey(tg => tg.TVShowId);

            builder
                .HasOne(tg => tg.Genre)
                .WithMany(g => g.TVShowGenres)
                .HasForeignKey(tg => tg.GenreId);
        }
    }
}
