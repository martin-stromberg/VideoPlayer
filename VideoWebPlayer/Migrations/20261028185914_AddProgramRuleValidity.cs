using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramRuleValidity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidFrom",
                table: "ProgramRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidFromDay",
                table: "ProgramRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidHolidayAfterDays",
                table: "ProgramRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidHolidayAnchor",
                table: "ProgramRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidHolidayBeforeDays",
                table: "ProgramRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "ProgramRules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidToDay",
                table: "ProgramRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidWeekdays",
                table: "ProgramRules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValidityMode",
                table: "ProgramRules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidFromDay",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidHolidayAfterDays",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidHolidayAnchor",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidHolidayBeforeDays",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidToDay",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidWeekdays",
                table: "ProgramRules");

            migrationBuilder.DropColumn(
                name: "ValidityMode",
                table: "ProgramRules");
        }
    }
}
