using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="Playlist"/>.
    /// </summary>
    public sealed class PlaylistConfiguration : IEntityTypeConfiguration<Playlist>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<Playlist> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(255)
                .UseCollation("NOCASE");

            builder.Property(p => p.Description)
                .HasMaxLength(2000);

            builder.Property(p => p.SortMode)
                .HasDefaultValue(PlaylistSortMode.ByReleaseDate);

            builder.Property(p => p.CreatedAt).IsRequired();
            builder.Property(p => p.UpdatedAt).IsRequired();

            builder
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => new { p.UserId, p.Name }).IsUnique();
        }
    }
}
