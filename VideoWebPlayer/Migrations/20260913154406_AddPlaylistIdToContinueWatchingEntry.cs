using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistIdToContinueWatchingEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_UserId_MovieId",
                table: "ContinueWatchingEntries");

            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_UserId_TVShowEpisodeId",
                table: "ContinueWatchingEntries");

            migrationBuilder.AddColumn<long>(
                name: "PlaylistId",
                table: "ContinueWatchingEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_PlaylistId",
                table: "ContinueWatchingEntries",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_MovieId_PlaylistId",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "MovieId", "PlaylistId" },
                unique: true,
                filter: "[MovieId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_TVShowEpisodeId_PlaylistId",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "TVShowEpisodeId", "PlaylistId" },
                unique: true,
                filter: "[TVShowEpisodeId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ContinueWatchingEntries_Playlists_PlaylistId",
                table: "ContinueWatchingEntries",
                column: "PlaylistId",
                principalTable: "Playlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContinueWatchingEntries_Playlists_PlaylistId",
                table: "ContinueWatchingEntries");

            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_PlaylistId",
                table: "ContinueWatchingEntries");

            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_UserId_MovieId_PlaylistId",
                table: "ContinueWatchingEntries");

            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_UserId_TVShowEpisodeId_PlaylistId",
                table: "ContinueWatchingEntries");

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "ContinueWatchingEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_MovieId",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "MovieId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_TVShowEpisodeId",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "TVShowEpisodeId" });
        }
    }
}
