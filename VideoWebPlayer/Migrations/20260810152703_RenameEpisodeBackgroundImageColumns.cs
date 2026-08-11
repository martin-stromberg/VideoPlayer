using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class RenameEpisodeBackgroundImageColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TVShowEpisodes_Pictures_GeneratedBackgroundImageId",
                table: "TVShowEpisodes");

            migrationBuilder.RenameColumn(
                name: "GeneratedBackgroundImageId",
                table: "TVShowEpisodes",
                newName: "GeneratedBackgroundPictureId");

            migrationBuilder.RenameIndex(
                name: "IX_TVShowEpisodes_GeneratedBackgroundImageId",
                table: "TVShowEpisodes",
                newName: "IX_TVShowEpisodes_GeneratedBackgroundPictureId");

            migrationBuilder.RenameColumn(
                name: "EpisodeIdReference",
                table: "Pictures",
                newName: "EpisodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Pictures_EpisodeIdReference_IsGeneratedBackground",
                table: "Pictures",
                newName: "IX_Pictures_EpisodeId_IsGeneratedBackground");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowEpisodes_Pictures_GeneratedBackgroundPictureId",
                table: "TVShowEpisodes",
                column: "GeneratedBackgroundPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TVShowEpisodes_Pictures_GeneratedBackgroundPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.RenameColumn(
                name: "GeneratedBackgroundPictureId",
                table: "TVShowEpisodes",
                newName: "GeneratedBackgroundImageId");

            migrationBuilder.RenameIndex(
                name: "IX_TVShowEpisodes_GeneratedBackgroundPictureId",
                table: "TVShowEpisodes",
                newName: "IX_TVShowEpisodes_GeneratedBackgroundImageId");

            migrationBuilder.RenameColumn(
                name: "EpisodeId",
                table: "Pictures",
                newName: "EpisodeIdReference");

            migrationBuilder.RenameIndex(
                name: "IX_Pictures_EpisodeId_IsGeneratedBackground",
                table: "Pictures",
                newName: "IX_Pictures_EpisodeIdReference_IsGeneratedBackground");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowEpisodes_Pictures_GeneratedBackgroundImageId",
                table: "TVShowEpisodes",
                column: "GeneratedBackgroundImageId",
                principalTable: "Pictures",
                principalColumn: "Id");
        }
    }
}
