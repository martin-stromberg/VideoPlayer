using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Client.Models
{
    /// <summary>
    /// Basic actor data for list and search results.
    /// </summary>
    public class ActorDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PictureUrl { get; set; }
    }

    /// <summary>
    /// Movie or episode reference on an actor details page.
    /// </summary>
    public class ActorMediaEntryDto
    {
        public string Type { get; set; } = string.Empty;
        public long Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? PictureUrl { get; set; }
    }

    /// <summary>
    /// Full actor details including aggregated media references.
    /// </summary>
    public class ActorDetailsDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PictureUrl { get; set; }
        public List<ActorMediaEntryDto> Media { get; set; } = new();
    }
}
