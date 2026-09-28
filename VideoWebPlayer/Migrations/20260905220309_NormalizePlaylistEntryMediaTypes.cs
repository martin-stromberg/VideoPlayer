using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePlaylistEntryMediaTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""PlaylistEntries"" SET ""MediaType"" = 'Movie' WHERE LOWER(""MediaType"") = 'movie';");
            migrationBuilder.Sql(@"UPDATE ""PlaylistEntries"" SET ""MediaType"" = 'TVShow' WHERE LOWER(""MediaType"") = 'tvshow';");
            migrationBuilder.Sql(@"UPDATE ""PlaylistEntries"" SET ""MediaType"" = 'TVShowSeason' WHERE LOWER(""MediaType"") = 'tvshowseason';");
            migrationBuilder.Sql(@"UPDATE ""PlaylistEntries"" SET ""MediaType"" = 'TVShowEpisode' WHERE LOWER(""MediaType"") = 'tvshowepisode';");
            migrationBuilder.Sql(@"UPDATE ""PlaylistEntries"" SET ""MediaType"" = 'MovieCollection' WHERE LOWER(""MediaType"") = 'moviecollection';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
