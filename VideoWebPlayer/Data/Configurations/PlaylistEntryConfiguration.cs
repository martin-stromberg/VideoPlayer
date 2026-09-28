using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="PlaylistEntry"/>.
    /// </summary>
    public sealed class PlaylistEntryConfiguration : IEntityTypeConfiguration<PlaylistEntry>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<PlaylistEntry> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.MediaType).IsRequired();
            builder.Property(e => e.AddedAt).IsRequired();

            builder
                .HasOne(e => e.Playlist)
                .WithMany(p => p.PlaylistEntries)
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.PlaylistId, e.MediaType, e.MediaId }).IsUnique();

            // Lookup "which playlists contain this collection medium" for the marker-driven backfill
            // (PlaylistBackfillService); the unique index above starts with PlaylistId and cannot serve it.
            builder.HasIndex(e => new { e.MediaType, e.MediaId })
                .HasDatabaseName("IX_PlaylistEntries_MediaType_MediaId");

            builder.HasIndex(e => new { e.PlaylistId, e.SortOrder })
                .HasDatabaseName("IX_PlaylistEntries_PlaylistId_SortOrder");
        }
    }
}
