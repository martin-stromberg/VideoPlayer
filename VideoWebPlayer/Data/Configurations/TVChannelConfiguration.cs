using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="TVChannel"/>.
    /// </summary>
    public sealed class TVChannelConfiguration : IEntityTypeConfiguration<TVChannel>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TVChannel> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(TVChannel.NameMaxLength)
                .UseCollation("NOCASE");

            builder.Property(c => c.CreatedAt).IsRequired();
            builder.Property(c => c.UpdatedAt).IsRequired();
        }
    }
}
