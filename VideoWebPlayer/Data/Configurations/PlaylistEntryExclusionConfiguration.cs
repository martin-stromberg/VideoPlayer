using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="PlaylistEntryExclusion"/>.
    /// </summary>
    public sealed class PlaylistEntryExclusionConfiguration : IEntityTypeConfiguration<PlaylistEntryExclusion>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<PlaylistEntryExclusion> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.MediaType).IsRequired();
            builder.Property(e => e.ExcludedAt).IsRequired();

            builder
                .HasOne(e => e.Playlist)
                .WithMany()
                .HasForeignKey(e => e.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.PlaylistId, e.MediaType, e.MediaId }).IsUnique();
        }
    }
}
