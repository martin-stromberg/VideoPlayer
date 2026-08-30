using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace VideoWebPlayer.Data.Configurations
{
    /// <summary>
    /// EF Core configuration for <see cref="TVShowEpisodeActor"/>.
    /// </summary>
    public sealed class TVShowEpisodeActorConfiguration : IEntityTypeConfiguration<TVShowEpisodeActor>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TVShowEpisodeActor> builder)
        {
            builder.HasKey(ea => new { ea.TVShowEpisodeId, ea.ActorId });

            builder
                .HasOne(ea => ea.TVShowEpisode)
                .WithMany(e => e.TVShowEpisodeActors)
                .HasForeignKey(ea => ea.TVShowEpisodeId);

            builder
                .HasOne(ea => ea.Actor)
                .WithMany(a => a.TVShowEpisodeActors)
                .HasForeignKey(ea => ea.ActorId);
        }
    }
}
