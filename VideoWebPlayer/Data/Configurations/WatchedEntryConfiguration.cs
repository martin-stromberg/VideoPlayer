using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations;

/// <summary>
/// EF Core configuration for <see cref="WatchedEntry"/>.
/// </summary>
public sealed class WatchedEntryConfiguration : IEntityTypeConfiguration<WatchedEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WatchedEntry> builder)
    {
        builder.ToTable("WatchedEntries", table =>
            table.HasCheckConstraint(
                "CK_WatchedEntries_ExactlyOneTitle",
                "((MovieId IS NOT NULL AND TVShowEpisodeId IS NULL) OR (MovieId IS NULL AND TVShowEpisodeId IS NOT NULL))"));

        builder.HasIndex(x => new { x.UserId, x.MovieId })
            .IsUnique()
            .HasFilter("[MovieId] IS NOT NULL");

        builder.HasIndex(x => new { x.UserId, x.TVShowEpisodeId })
            .IsUnique()
            .HasFilter("[TVShowEpisodeId] IS NOT NULL");

        builder.HasIndex(x => x.UserId);

        builder
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Movie)
            .WithMany()
            .HasForeignKey(x => x.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.TVShowEpisode)
            .WithMany()
            .HasForeignKey(x => x.TVShowEpisodeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
