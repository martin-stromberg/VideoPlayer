using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramRule"/>.
    /// </summary>
    public sealed class ProgramRuleConfiguration : IEntityTypeConfiguration<ProgramRule>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramRule> builder)
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(ProgramRule.NameMaxLength)
                .UseCollation("NOCASE");

            builder.Property(r => r.RuleType).IsRequired();
            builder.Property(r => r.Recurrence).IsRequired();
            builder.Property(r => r.TimeOfDay).IsRequired();
            builder.Property(r => r.MaterialType).IsRequired();
            builder.Property(r => r.MaterialId).IsRequired();
            builder.Property(r => r.Priority).IsRequired().HasDefaultValue(0);
            builder.Property(r => r.IsActive).IsRequired().HasDefaultValue(true);
            builder.Property(r => r.CreatedAt).IsRequired();
            builder.Property(r => r.UpdatedAt).IsRequired();

            builder
                .HasOne(r => r.TVChannel)
                .WithMany(c => c.ProgramRules)
                .HasForeignKey(r => r.TVChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => new { r.TVChannelId, r.Name }).IsUnique();
            builder.HasIndex(r => r.TVChannelId);
        }
    }
}
