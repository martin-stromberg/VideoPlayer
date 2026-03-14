using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FanartPictureId",
                table: "TVShowSeasons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FanartPictureId",
                table: "TVShows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FanartPictureId",
                table: "TVShowEpisodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FanartPictureId",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FanartPictureId",
                table: "MovieCollections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TVShowSeasons_FanartPictureId",
                table: "TVShowSeasons",
                column: "FanartPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShows_FanartPictureId",
                table: "TVShows",
                column: "FanartPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodes_FanartPictureId",
                table: "TVShowEpisodes",
                column: "FanartPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_FanartPictureId",
                table: "Movies",
                column: "FanartPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieCollections_FanartPictureId",
                table: "MovieCollections",
                column: "FanartPictureId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovieCollections_Pictures_FanartPictureId",
                table: "MovieCollections",
                column: "FanartPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Pictures_FanartPictureId",
                table: "Movies",
                column: "FanartPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowEpisodes_Pictures_FanartPictureId",
                table: "TVShowEpisodes",
                column: "FanartPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShows_Pictures_FanartPictureId",
                table: "TVShows",
                column: "FanartPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowSeasons_Pictures_FanartPictureId",
                table: "TVShowSeasons",
                column: "FanartPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovieCollections_Pictures_FanartPictureId",
                table: "MovieCollections");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Pictures_FanartPictureId",
                table: "Movies");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShowEpisodes_Pictures_FanartPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShows_Pictures_FanartPictureId",
                table: "TVShows");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShowSeasons_Pictures_FanartPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropIndex(
                name: "IX_TVShowSeasons_FanartPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropIndex(
                name: "IX_TVShows_FanartPictureId",
                table: "TVShows");

            migrationBuilder.DropIndex(
                name: "IX_TVShowEpisodes_FanartPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_Movies_FanartPictureId",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_MovieCollections_FanartPictureId",
                table: "MovieCollections");

            migrationBuilder.DropColumn(
                name: "FanartPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropColumn(
                name: "FanartPictureId",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "FanartPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "FanartPictureId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "FanartPictureId",
                table: "MovieCollections");
        }
    }
}
