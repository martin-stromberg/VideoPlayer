using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramRuleClipGroup"/>.
    /// </summary>
    public sealed class ProgramRuleClipGroupConfiguration : IEntityTypeConfiguration<ProgramRuleClipGroup>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramRuleClipGroup> builder)
        {
            builder.HasKey(l => new { l.ProgramRuleId, l.ProgramClipGroupId });

            builder
                .HasOne(l => l.ProgramRule)
                .WithMany(r => r.ClipGroups)
                .HasForeignKey(l => l.ProgramRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(l => l.ClipGroup)
                .WithMany()
                .HasForeignKey(l => l.ProgramClipGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
