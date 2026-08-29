using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations;

/// <summary>
/// EF Core configuration for <see cref="UnlockedMediaEntry"/>.
/// </summary>
public sealed class UnlockedMediaEntryConfiguration : IEntityTypeConfiguration<UnlockedMediaEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UnlockedMediaEntry> builder)
    {
        builder.HasIndex(u => new { u.UserId, u.MovieCollectionId, u.TVShowId }).IsUnique();
        builder.HasOne(u => u.User)
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
