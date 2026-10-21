using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramRuleCursor"/>.
    /// </summary>
    public sealed class ProgramRuleCursorConfiguration : IEntityTypeConfiguration<ProgramRuleCursor>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramRuleCursor> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.LastContentType).IsRequired();
            builder.Property(c => c.LastContentId).IsRequired();
            builder.Property(c => c.UpdatedAt).IsRequired();

            builder
                .HasOne(c => c.ProgramRule)
                .WithMany()
                .HasForeignKey(c => c.ProgramRuleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(c => c.ProgramRuleId).IsUnique();
        }
    }
}
