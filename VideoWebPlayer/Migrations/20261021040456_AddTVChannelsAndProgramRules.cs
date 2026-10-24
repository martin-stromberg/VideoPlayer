using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddTVChannelsAndProgramRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HolidayFederalState",
                table: "Setups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TVChannels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false, collation: "NOCASE"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TVChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TVChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false, collation: "NOCASE"),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    Recurrence = table.Column<int>(type: "INTEGER", nullable: false),
                    DayOfWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    SingleDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    TimeOfDay = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: true),
                    SlotDuration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    MaterialType = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<long>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramRules_TVChannels_TVChannelId",
                        column: x => x.TVChannelId,
                        principalTable: "TVChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRules_TVChannelId",
                table: "ProgramRules",
                column: "TVChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRules_TVChannelId_Name",
                table: "ProgramRules",
                columns: new[] { "TVChannelId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramRules");

            migrationBuilder.DropTable(
                name: "TVChannels");

            migrationBuilder.DropColumn(
                name: "HolidayFederalState",
                table: "Setups");
        }
    }
}
