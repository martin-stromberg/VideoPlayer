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
                .HasMaxLength(Playlist.NameMaxLength)
                .UseCollation("NOCASE");

            builder.Property(p => p.Description)
                .HasMaxLength(Playlist.DescriptionMaxLength);

            builder.Property(p => p.SortMode)
                .HasDefaultValue(PlaylistSortMode.ByReleaseDate);

            builder.Property(p => p.CreatedAt).IsRequired();
            builder.Property(p => p.UpdatedAt).IsRequired();

            builder
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Analog zum Muster TVShowEpisode.GeneratedBackgroundPicture (siehe TVShowEpisodeConfiguration):
            // Der Fremdschluessel liegt auf der Playlist-Seite (CoverPictureId), keine explizite
            // OnDelete-Angabe noetig (Standardverhalten fuer optionale Beziehungen reicht aus, da
            // PlaylistService.DeletePlaylistAsync das referenzierte Cover-Picture bereits selbst loescht).
            builder
                .HasOne(p => p.CoverPicture)
                .WithMany()
                .HasForeignKey(p => p.CoverPictureId);

            builder.HasIndex(p => new { p.UserId, p.Name }).IsUnique();
        }
    }
}
