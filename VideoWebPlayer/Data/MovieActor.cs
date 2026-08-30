namespace VideoWebPlayer.Data
{
    /// <summary>
    /// Join entity linking a movie to an actor.
    /// </summary>
    public class MovieActor
    {
        /// <summary>
        /// Gets or sets the movie identifier.
        /// </summary>
        public long MovieId { get; set; }

        /// <summary>
        /// Gets or sets the movie.
        /// </summary>
        public Movie Movie { get; set; } = null!;

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
