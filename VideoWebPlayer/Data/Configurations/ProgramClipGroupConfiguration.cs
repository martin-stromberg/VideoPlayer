using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="ProgramClipGroup"/>.
    /// </summary>
    public sealed class ProgramClipGroupConfiguration : IEntityTypeConfiguration<ProgramClipGroup>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ProgramClipGroup> builder)
        {
            builder.HasKey(g => g.Id);

            builder.Property(g => g.Name)
                .IsRequired()
                .HasMaxLength(ProgramClipGroup.NameMaxLength)
                .UseCollation("NOCASE");

            builder.Property(g => g.IsSystem).IsRequired().HasDefaultValue(false);
            builder.Property(g => g.CreatedAt).IsRequired();
            builder.Property(g => g.UpdatedAt).IsRequired();

            builder.HasIndex(g => g.Name).IsUnique();
        }
    }
}
