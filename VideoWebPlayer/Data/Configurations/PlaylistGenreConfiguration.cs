using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="PlaylistGenre"/>.
    /// </summary>
    public sealed class PlaylistGenreConfiguration : IEntityTypeConfiguration<PlaylistGenre>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<PlaylistGenre> builder)
        {
            builder.HasKey(pg => pg.Id);

            builder.Property(pg => pg.Count).IsRequired();

            builder
                .HasOne(pg => pg.Playlist)
                .WithMany()
                .HasForeignKey(pg => pg.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(pg => pg.Genre)
                .WithMany()
                .HasForeignKey(pg => pg.GenreId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(pg => new { pg.PlaylistId, pg.GenreId }).IsUnique();
        }
    }
}
