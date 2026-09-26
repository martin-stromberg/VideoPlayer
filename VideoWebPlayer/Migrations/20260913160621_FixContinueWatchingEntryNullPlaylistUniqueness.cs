using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class FixContinueWatchingEntryNullPlaylistUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_MovieId_NoPlaylist",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "MovieId" },
                unique: true,
                filter: "[MovieId] IS NOT NULL AND [PlaylistId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_TVShowEpisodeId_NoPlaylist",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "TVShowEpisodeId" },
                unique: true,
                filter: "[TVShowEpisodeId] IS NOT NULL AND [PlaylistId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_UserId_MovieId_NoPlaylist",
                table: "ContinueWatchingEntries");

            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_UserId_TVShowEpisodeId_NoPlaylist",
                table: "ContinueWatchingEntries");
        }
    }
}
