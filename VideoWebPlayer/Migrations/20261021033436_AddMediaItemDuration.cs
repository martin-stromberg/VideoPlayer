using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaItemDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "Duration",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DurationProbedAt",
                table: "MediaItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DurationProbedAt",
                table: "MediaItems",
                column: "DurationProbedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_DurationProbedAt",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "DurationProbedAt",
                table: "MediaItems");
        }
    }
}
