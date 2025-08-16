using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Premiered",
                table: "TVShows",
                newName: "ReleaseDate");

            migrationBuilder.AddColumn<DateTime>(
                name: "PremieredAt",
                table: "TVShowSeasons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseDate",
                table: "TVShowSeasons",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PremieredAt",
                table: "TVShows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PremieredAt",
                table: "MovieCollections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseDate",
                table: "MovieCollections",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PremieredAt",
                table: "TVShowSeasons");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "TVShowSeasons");

            migrationBuilder.DropColumn(
                name: "PremieredAt",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "PremieredAt",
                table: "MovieCollections");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "MovieCollections");

            migrationBuilder.RenameColumn(
                name: "ReleaseDate",
                table: "TVShows",
                newName: "Premiered");
        }
    }
}
