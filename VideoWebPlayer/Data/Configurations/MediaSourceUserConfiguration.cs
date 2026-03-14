using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="MediaSourceUser"/>.
    /// </summary>
    public sealed class MediaSourceUserConfiguration : IEntityTypeConfiguration<MediaSourceUser>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<MediaSourceUser> builder)
        {
            builder.HasKey(msu => new { msu.MediaSourceId, msu.UserId });
        }
    }
}
