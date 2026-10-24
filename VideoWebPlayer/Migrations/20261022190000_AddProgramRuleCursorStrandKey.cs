using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramRuleCursorStrandKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramRuleCursors_ProgramRuleId",
                table: "ProgramRuleCursors");

            migrationBuilder.AddColumn<string>(
                name: "StrandKey",
                table: "ProgramRuleCursors",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRuleCursors_ProgramRuleId_StrandKey",
                table: "ProgramRuleCursors",
                columns: new[] { "ProgramRuleId", "StrandKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramRuleCursors_ProgramRuleId_StrandKey",
                table: "ProgramRuleCursors");

            migrationBuilder.DropColumn(
                name: "StrandKey",
                table: "ProgramRuleCursors");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRuleCursors_ProgramRuleId",
                table: "ProgramRuleCursors",
                column: "ProgramRuleId",
                unique: true);
        }
    }
}
