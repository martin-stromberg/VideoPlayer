using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramSlot"/>.
    /// </summary>
    public sealed class ProgramSlotConfiguration : IEntityTypeConfiguration<ProgramSlot>
    {
        // SQLite stores DateTime as text without kind information, so EF would materialize
        // Unspecified — ambiguous for consumers that interpret the kind (DateTimeOffset
        // construction, ToUniversalTime(), serialization) on values that are UTC by contract.
        // The read side of the converter declares them UTC; the write side stays identity,
        // so the stored format and existing rows are unchanged.
        private static readonly ValueConverter<DateTime, DateTime> UtcOnRead =
            new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramSlot> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Start).IsRequired().HasConversion(UtcOnRead);
            builder.Property(s => s.End).IsRequired().HasConversion(UtcOnRead);
            builder.Property(s => s.MediaItemId).IsRequired();
            builder.Property(s => s.RuleType).IsRequired();
            builder.Property(s => s.Status).IsRequired();
            builder.Property(s => s.IsClipFill).IsRequired();
            builder.Property(s => s.CreatedAt).IsRequired().HasConversion(UtcOnRead);

            builder
                .HasOne(s => s.TVChannel)
                .WithMany()
                .HasForeignKey(s => s.TVChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => new { s.TVChannelId, s.Start });
        }
    }
}
