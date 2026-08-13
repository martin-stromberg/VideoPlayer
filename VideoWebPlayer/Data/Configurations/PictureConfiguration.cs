using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="Picture"/>.
    /// </summary>
    public sealed class PictureConfiguration : IEntityTypeConfiguration<Picture>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<Picture> builder)
        {
            builder
                .HasIndex(p => new { p.EpisodeId, p.IsGeneratedBackground });
        }
    }
}
