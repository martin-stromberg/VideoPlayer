using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSortOrderToPlaylistEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SortOrder",
                table: "PlaylistEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistEntries_PlaylistId_SortOrder",
                table: "PlaylistEntries",
                columns: new[] { "PlaylistId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlaylistEntries_PlaylistId_SortOrder",
                table: "PlaylistEntries");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "PlaylistEntries");
        }
    }
}
