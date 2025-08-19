using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels23 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "TVShowSeasons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "TVShows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "TVShowEpisodes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "MovieCollections",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "TVShowSeasons");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "MovieCollections");
        }
    }
}
