using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramClipGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "PlaceholderGrace",
                table: "ProgramRules",
                type: "TEXT",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 15, 0, 0));

            migrationBuilder.CreateTable(
                name: "ProgramClipGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false, collation: "NOCASE"),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramClipGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProgramClipGroupItems",
                columns: table => new
                {
                    ProgramClipGroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaItemId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramClipGroupItems", x => new { x.ProgramClipGroupId, x.MediaItemId });
                    table.ForeignKey(
                        name: "FK_ProgramClipGroupItems_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramClipGroupItems_ProgramClipGroups_ProgramClipGroupId",
                        column: x => x.ProgramClipGroupId,
                        principalTable: "ProgramClipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramRuleClipGroups",
                columns: table => new
                {
                    ProgramRuleId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProgramClipGroupId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramRuleClipGroups", x => new { x.ProgramRuleId, x.ProgramClipGroupId });
                    table.ForeignKey(
                        name: "FK_ProgramRuleClipGroups_ProgramClipGroups_ProgramClipGroupId",
                        column: x => x.ProgramClipGroupId,
                        principalTable: "ProgramClipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramRuleClipGroups_ProgramRules_ProgramRuleId",
                        column: x => x.ProgramRuleId,
                        principalTable: "ProgramRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramClipGroupItems_MediaItemId",
                table: "ProgramClipGroupItems",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramClipGroups_Name",
                table: "ProgramClipGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRuleClipGroups_ProgramClipGroupId",
                table: "ProgramRuleClipGroups",
                column: "ProgramClipGroupId");

            // Reserved system group "Trailer" — filled automatically with trailers of upcoming
            // movie airings; runs only under Migrate() (EnsureCreated fixtures seed it via
            // SeedTrailerClipGroupAsync).
            migrationBuilder.InsertData(
                table: "ProgramClipGroups",
                columns: new[] { "Id", "Name", "IsSystem", "CreatedAt", "UpdatedAt" },
                values: new object[] { 1L, "Trailer", true, new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramClipGroupItems");

            migrationBuilder.DropTable(
                name: "ProgramRuleClipGroups");

            migrationBuilder.DropTable(
                name: "ProgramClipGroups");

            migrationBuilder.DropColumn(
                name: "PlaceholderGrace",
                table: "ProgramRules");
        }
    }
}
