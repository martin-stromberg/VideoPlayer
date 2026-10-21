using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramSlot"/>.
    /// </summary>
    public sealed class ProgramSlotConfiguration : IEntityTypeConfiguration<ProgramSlot>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramSlot> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Start).IsRequired();
            builder.Property(s => s.End).IsRequired();
            builder.Property(s => s.MediaItemId).IsRequired();
            builder.Property(s => s.RuleType).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired();

            builder
                .HasOne(s => s.TVChannel)
                .WithMany()
                .HasForeignKey(s => s.TVChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => new { s.TVChannelId, s.Start });
        }
    }
}
