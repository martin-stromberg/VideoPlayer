using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramScheduleAgeWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "ProgramScheduleAgeWindow12Local",
                table: "Setups",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(20, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ProgramScheduleAgeWindow16Local",
                table: "Setups",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(22, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ProgramScheduleAgeWindow18Local",
                table: "Setups",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(23, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ProgramScheduleAgeWindowNightEndLocal",
                table: "Setups",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeOnly(6, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "ProgramScheduleUnknownRatingMode",
                table: "Setups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProgramScheduleAgeWindow12Local",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "ProgramScheduleAgeWindow16Local",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "ProgramScheduleAgeWindow18Local",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "ProgramScheduleAgeWindowNightEndLocal",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "ProgramScheduleUnknownRatingMode",
                table: "Setups");
        }
    }
}
