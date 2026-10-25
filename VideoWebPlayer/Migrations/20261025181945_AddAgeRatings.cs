using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddAgeRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgeRating",
                table: "TVShows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgeRatingClassifiedAt",
                table: "TVShows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgeRatingMinAge",
                table: "TVShows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgeRatingRaw",
                table: "TVShows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgeRating",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgeRatingClassifiedAt",
                table: "Movies",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgeRatingMinAge",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgeRatingRaw",
                table: "Movies",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgeRating",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "AgeRatingClassifiedAt",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "AgeRatingMinAge",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "AgeRatingRaw",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "AgeRating",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AgeRatingClassifiedAt",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AgeRatingMinAge",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "AgeRatingRaw",
                table: "Movies");
        }
    }
}
