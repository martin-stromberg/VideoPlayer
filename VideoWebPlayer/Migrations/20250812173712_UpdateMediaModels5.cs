using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BannerPictureId",
                table: "TVShowSeasons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PosterPictureId",
                table: "TVShowSeasons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BannerPictureId",
                table: "TVShows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PosterPictureId",
                table: "TVShows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BannerPictureId",
                table: "TVShowEpisodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PosterPictureId",
                table: "TVShowEpisodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BannerPictureId",
                table: "MovieCollections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PosterPictureId",
                table: "MovieCollections",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TVShowSeasons_BannerPictureId",
                table: "TVShowSeasons",
                column: "BannerPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowSeasons_PosterPictureId",
                table: "TVShowSeasons",
                column: "PosterPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShows_BannerPictureId",
                table: "TVShows",
                column: "BannerPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShows_PosterPictureId",
                table: "TVShows",
                column: "PosterPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodes_BannerPictureId",
                table: "TVShowEpisodes",
                column: "BannerPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodes_PosterPictureId",
                table: "TVShowEpisodes",
                column: "PosterPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieCollections_BannerPictureId",
                table: "MovieCollections",
                column: "BannerPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieCollections_PosterPictureId",
                table: "MovieCollections",
                column: "PosterPictureId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovieCollections_Pictures_BannerPictureId",
                table: "MovieCollections",
                column: "BannerPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MovieCollections_Pictures_PosterPictureId",
                table: "MovieCollections",
                column: "PosterPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowEpisodes_Pictures_BannerPictureId",
                table: "TVShowEpisodes",
                column: "BannerPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowEpisodes_Pictures_PosterPictureId",
                table: "TVShowEpisodes",
                column: "PosterPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShows_Pictures_BannerPictureId",
                table: "TVShows",
                column: "BannerPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShows_Pictures_PosterPictureId",
                table: "TVShows",
                column: "PosterPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowSeasons_Pictures_BannerPictureId",
                table: "TVShowSeasons",
                column: "BannerPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowSeasons_Pictures_PosterPictureId",
                table: "TVShowSeasons",
                column: "PosterPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovieCollections_Pictures_BannerPictureId",
                table: "MovieCollections");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieCollections_Pictures_PosterPictureId",
                table: "MovieCollections");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShowEpisodes_Pictures_BannerPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShowEpisodes_Pictures_PosterPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShows_Pictures_BannerPictureId",
                table: "TVShows");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShows_Pictures_PosterPictureId",
                table: "TVShows");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShowSeasons_Pictures_BannerPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShowSeasons_Pictures_PosterPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropIndex(
                name: "IX_TVShowSeasons_BannerPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropIndex(
                name: "IX_TVShowSeasons_PosterPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropIndex(
                name: "IX_TVShows_BannerPictureId",
                table: "TVShows");

            migrationBuilder.DropIndex(
                name: "IX_TVShows_PosterPictureId",
                table: "TVShows");

            migrationBuilder.DropIndex(
                name: "IX_TVShowEpisodes_BannerPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_TVShowEpisodes_PosterPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_MovieCollections_BannerPictureId",
                table: "MovieCollections");

            migrationBuilder.DropIndex(
                name: "IX_MovieCollections_PosterPictureId",
                table: "MovieCollections");

            migrationBuilder.DropColumn(
                name: "BannerPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropColumn(
                name: "PosterPictureId",
                table: "TVShowSeasons");

            migrationBuilder.DropColumn(
                name: "BannerPictureId",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "PosterPictureId",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "BannerPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "PosterPictureId",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "BannerPictureId",
                table: "MovieCollections");

            migrationBuilder.DropColumn(
                name: "PosterPictureId",
                table: "MovieCollections");
        }
    }
}
