using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddEpisodeBackgroundImageProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BackgroundImageGeneratedAt",
                table: "TVShowEpisodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BackgroundImageRequiresUpdate",
                table: "TVShowEpisodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "GeneratedBackgroundImageId",
                table: "TVShowEpisodes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodes_GeneratedBackgroundImageId",
                table: "TVShowEpisodes",
                column: "GeneratedBackgroundImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_TVShowEpisodes_Pictures_GeneratedBackgroundImageId",
                table: "TVShowEpisodes",
                column: "GeneratedBackgroundImageId",
                principalTable: "Pictures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TVShowEpisodes_Pictures_GeneratedBackgroundImageId",
                table: "TVShowEpisodes");

            migrationBuilder.DropIndex(
                name: "IX_TVShowEpisodes_GeneratedBackgroundImageId",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "BackgroundImageGeneratedAt",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "BackgroundImageRequiresUpdate",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "GeneratedBackgroundImageId",
                table: "TVShowEpisodes");
        }
    }
}
