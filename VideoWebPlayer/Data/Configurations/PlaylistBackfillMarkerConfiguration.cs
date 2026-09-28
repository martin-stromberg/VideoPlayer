using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="PlaylistBackfillMarker"/>.
    /// </summary>
    public sealed class PlaylistBackfillMarkerConfiguration : IEntityTypeConfiguration<PlaylistBackfillMarker>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<PlaylistBackfillMarker> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.MediaType).IsRequired();
            builder.Property(e => e.MarkedAt).IsRequired();
            builder.Property(e => e.Version).IsRequired();

            builder.HasIndex(e => new { e.MediaType, e.MediaId }).IsUnique();
        }
    }
}
