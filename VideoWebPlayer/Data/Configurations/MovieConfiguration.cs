using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="Movie"/>.
    /// </summary>
    public sealed class MovieConfiguration : IEntityTypeConfiguration<Movie>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<Movie> builder)
        {
            builder.Ignore(m => m.MediaItems);

            builder
                .HasOne(m => m.MovieCollection)
                .WithMany(mc => mc.Movies)
                .HasForeignKey(m => m.MovieCollectionId);
        }
    }
}
