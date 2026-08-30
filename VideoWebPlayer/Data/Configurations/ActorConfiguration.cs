using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="Actor"/>.
    /// </summary>
    public sealed class ActorConfiguration : IEntityTypeConfiguration<Actor>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<Actor> builder)
        {
            builder
                .HasOne(a => a.Picture)
                .WithMany()
                .HasForeignKey(a => a.PictureId);

            builder.HasIndex(a => a.NormalizedName);
        }
    }
}
