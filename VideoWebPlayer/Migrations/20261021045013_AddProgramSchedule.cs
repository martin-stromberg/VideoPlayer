using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProgramScheduleHorizonHours",
                table: "Setups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgramScheduleIntervalMinutes",
                table: "Setups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProgramRuleCursors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProgramRuleId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    LastContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramRuleCursors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramRuleCursors_ProgramRules_ProgramRuleId",
                        column: x => x.ProgramRuleId,
                        principalTable: "ProgramRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramSlots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TVChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    Start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    End = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MediaItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    MovieId = table.Column<long>(type: "INTEGER", nullable: true),
                    TVShowEpisodeId = table.Column<long>(type: "INTEGER", nullable: true),
                    ProgramRuleId = table.Column<long>(type: "INTEGER", nullable: true),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramSlots_TVChannels_TVChannelId",
                        column: x => x.TVChannelId,
                        principalTable: "TVChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRuleCursors_ProgramRuleId",
                table: "ProgramRuleCursors",
                column: "ProgramRuleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramSlots_TVChannelId_Start",
                table: "ProgramSlots",
                columns: new[] { "TVChannelId", "Start" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramRuleCursors");

            migrationBuilder.DropTable(
                name: "ProgramSlots");

            migrationBuilder.DropColumn(
                name: "ProgramScheduleHorizonHours",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "ProgramScheduleIntervalMinutes",
                table: "Setups");
        }
    }
}
