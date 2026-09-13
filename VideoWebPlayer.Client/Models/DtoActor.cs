using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Basic actor data for list and search results.
    /// </summary>
    public class ActorDto
    {
        /// <summary>
        /// Gets or sets the actor's unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the actor's name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL of the actor's picture, or <c>null</c> if none is set.
        /// </summary>
        public string? PictureUrl { get; set; }

        /// <summary>
        /// Gets or sets the role or character name played by the actor, if known.
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Gets or sets the display order of the actor within its cast list.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Gets or sets the total number of videos the actor appears in.
        /// </summary>
        public int VideoCount { get; set; }

        /// <summary>
        /// Gets or sets the number of videos the actor appears in within the current context
        /// (e.g. the movie or episode being viewed).
        /// </summary>
        public int ContextVideoCount { get; set; }
    }

    /// <summary>
    /// Movie or episode reference on an actor details page.
    /// </summary>
    public class ActorMediaEntryDto
    {
        /// <summary>
        /// Gets or sets the media type of the referenced entry (e.g. movie or episode).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the identifier of the referenced media entry.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the title of the referenced media entry.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional subtitle for the referenced media entry (e.g. season and
        /// episode number), or <c>null</c> if not applicable.
        /// </summary>
        public string? Subtitle { get; set; }

        /// <summary>
        /// Gets or sets the URL of the poster or thumbnail picture, or <c>null</c> if none is set.
        /// </summary>
        public string? PictureUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL to navigate to when the entry is selected, or <c>null</c> if not
        /// navigable.
        /// </summary>
        public string? LinkUrl { get; set; }
    }

    /// <summary>
    /// Full actor details including aggregated media references.
    /// </summary>
    public class ActorDetailsDto
    {
        /// <summary>
        /// Gets or sets the actor's unique identifier.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the actor's name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL of the actor's picture, or <c>null</c> if none is set.
        /// </summary>
        public string? PictureUrl { get; set; }

        /// <summary>
        /// Gets or sets the movie and episode references the actor appears in.
        /// </summary>
        /// <value>The list of media references.</value>
        public List<ActorMediaEntryDto> Media { get; set; } = new();
    }
}
