namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Join entity linking a TV show episode to an actor.
    /// </summary>
    public class TVShowEpisodeActor
    {
        /// <summary>
        /// Gets or sets the TV show episode identifier.
        /// </summary>
        public long TVShowEpisodeId { get; set; }

        /// <summary>
        /// Gets or sets the TV show episode.
        /// </summary>
        public TVShowEpisode TVShowEpisode { get; set; } = null!;

        /// <summary>
        /// Gets or sets the actor identifier.
        /// </summary>
        public long ActorId { get; set; }

        /// <summary>
        /// Gets or sets the actor.
        /// </summary>
        public Actor Actor { get; set; } = null!;
    }
}
