using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramClipGroupItem"/>.
    /// </summary>
    public sealed class ProgramClipGroupItemConfiguration : IEntityTypeConfiguration<ProgramClipGroupItem>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramClipGroupItem> builder)
        {
            builder.HasKey(i => new { i.ProgramClipGroupId, i.MediaItemId });

            builder
                .HasOne(i => i.ProgramClipGroup)
                .WithMany()
                .HasForeignKey(i => i.ProgramClipGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(i => i.MediaItem)
                .WithMany()
                .HasForeignKey(i => i.MediaItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
