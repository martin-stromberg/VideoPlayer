using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramRuleMediaSource"/>.
    /// </summary>
    public sealed class ProgramRuleMediaSourceConfiguration : IEntityTypeConfiguration<ProgramRuleMediaSource>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramRuleMediaSource> builder)
        {
            builder.HasKey(s => new { s.ProgramRuleId, s.MediaSourceId });

            builder
                .HasOne(s => s.ProgramRule)
                .WithMany(r => r.MediaSourceFilters)
                .HasForeignKey(s => s.ProgramRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(s => s.MediaSource)
                .WithMany()
                .HasForeignKey(s => s.MediaSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
