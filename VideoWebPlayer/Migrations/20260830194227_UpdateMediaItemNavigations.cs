using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaItemNavigations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_Movies_MovieId",
                table: "MediaItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_TVShowEpisodes_TVShowEpisodeId",
                table: "MediaItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MovieMediaItems_Movies_MovieId1",
                table: "MovieMediaItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TVShowEpisodeMediaItems_TVShowEpisodes_TVShowEpisodeId1",
                table: "TVShowEpisodeMediaItems");

            migrationBuilder.DropIndex(
                name: "IX_TVShowEpisodeMediaItems_TVShowEpisodeId1",
                table: "TVShowEpisodeMediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MovieMediaItems_MovieId1",
                table: "MovieMediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_MovieId",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_TVShowEpisodeId",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "TVShowEpisodeId1",
                table: "TVShowEpisodeMediaItems");

            migrationBuilder.DropColumn(
                name: "MovieId1",
                table: "MovieMediaItems");

            migrationBuilder.DropColumn(
                name: "MovieId",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "TVShowEpisodeId",
                table: "MediaItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TVShowEpisodeId1",
                table: "TVShowEpisodeMediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MovieId1",
                table: "MovieMediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MovieId",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TVShowEpisodeId",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodeMediaItems_TVShowEpisodeId1",
                table: "TVShowEpisodeMediaItems",
                column: "TVShowEpisodeId1");

            migrationBuilder.CreateIndex(
                name: "IX_MovieMediaItems_MovieId1",
                table: "MovieMediaItems",
                column: "MovieId1");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_MovieId",
                table: "MediaItems",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_TVShowEpisodeId",
                table: "MediaItems",
                column: "TVShowEpisodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_Movies_MovieId",
                table: "MediaItems",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_TVShowEpisodes_TVShowEpisodeId",
                table: "MediaItems",
                column: "TVShowEpisodeId",
                principalTable: "TVShowEpisodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MovieMediaItems_Movies_MovieId1",
                table: "MovieMediaItems",
                column: "MovieId1",
                principalTable: "Movies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowEpisodeMediaItems_TVShowEpisodes_TVShowEpisodeId1",
                table: "TVShowEpisodeMediaItems",
                column: "TVShowEpisodeId1",
                principalTable: "TVShowEpisodes",
                principalColumn: "Id");
        }
    }
}
