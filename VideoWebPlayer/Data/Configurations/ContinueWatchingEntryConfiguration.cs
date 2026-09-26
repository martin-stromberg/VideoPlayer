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
            builder.HasIndex(x => new { x.UserId, x.MovieId, x.PlaylistId })
                .IsUnique()
                .HasFilter("[MovieId] IS NOT NULL");

            builder.HasIndex(x => new { x.UserId, x.TVShowEpisodeId, x.PlaylistId })
                .IsUnique()
                .HasFilter("[TVShowEpisodeId] IS NOT NULL");

            // Die beiden obigen Indizes greifen wegen der SQL-Standard-NULL-Semantik nicht fuer den
            // Playlist-losen Fall (PlaylistId = NULL werden in einem Unique-Index als paarweise
            // verschieden behandelt). Diese beiden zusaetzlichen, auf PlaylistId IS NULL gefilterten
            // Indizes stellen die Eindeutigkeit von (UserId, MovieId) bzw. (UserId, TVShowEpisodeId)
            // fuer Eintraege OHNE Playlist-Bezug sicher.
            builder.HasIndex(x => new { x.UserId, x.MovieId })
                .IsUnique()
                .HasDatabaseName("IX_ContinueWatchingEntries_UserId_MovieId_NoPlaylist")
                .HasFilter("[MovieId] IS NOT NULL AND [PlaylistId] IS NULL");

            builder.HasIndex(x => new { x.UserId, x.TVShowEpisodeId })
                .IsUnique()
                .HasDatabaseName("IX_ContinueWatchingEntries_UserId_TVShowEpisodeId_NoPlaylist")
                .HasFilter("[TVShowEpisodeId] IS NOT NULL AND [PlaylistId] IS NULL");

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

            builder
                .HasOne(x => x.Playlist)
                .WithMany()
                .HasForeignKey(x => x.PlaylistId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
