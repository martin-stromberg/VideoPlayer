using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramRuleGenre"/>.
    /// </summary>
    public sealed class ProgramRuleGenreConfiguration : IEntityTypeConfiguration<ProgramRuleGenre>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramRuleGenre> builder)
        {
            builder.HasKey(g => new { g.ProgramRuleId, g.GenreId });

            builder
                .HasOne(g => g.ProgramRule)
                .WithMany(r => r.Genres)
                .HasForeignKey(g => g.ProgramRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(g => g.Genre)
                .WithMany()
                .HasForeignKey(g => g.GenreId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
