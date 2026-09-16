using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistCoverFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CoverPictureId",
                table: "Playlists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CoverPictureIsUserUploaded",
                table: "Playlists",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PlaylistId",
                table: "Pictures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_CoverPictureId",
                table: "Playlists",
                column: "CoverPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_Pictures_PlaylistId_IsGeneratedBackground",
                table: "Pictures",
                columns: new[] { "PlaylistId", "IsGeneratedBackground" });

            migrationBuilder.AddForeignKey(
                name: "FK_Playlists_Pictures_CoverPictureId",
                table: "Playlists",
                column: "CoverPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Playlists_Pictures_CoverPictureId",
                table: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Playlists_CoverPictureId",
                table: "Playlists");

            migrationBuilder.DropIndex(
                name: "IX_Pictures_PlaylistId_IsGeneratedBackground",
                table: "Pictures");

            migrationBuilder.DropColumn(
                name: "CoverPictureId",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "CoverPictureIsUserUploaded",
                table: "Playlists");

            migrationBuilder.DropColumn(
                name: "PlaylistId",
                table: "Pictures");
        }
    }
}
