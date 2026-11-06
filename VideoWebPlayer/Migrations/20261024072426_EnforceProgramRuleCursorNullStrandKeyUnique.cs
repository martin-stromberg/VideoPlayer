using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class EnforceProgramRuleCursorNullStrandKeyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration first: merge duplicate NULL-StrandKey rows per ProgramRuleId
            // (SQLite treats NULLs as distinct, so the composite unique index never covered
            // them). The youngest row wins — highest UpdatedAt, ties broken by highest Id —
            // the same winner rule the restore deduplication and the cursor read paths use.
            migrationBuilder.Sql(
                """
                DELETE FROM "ProgramRuleCursors"
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (
                                   PARTITION BY "ProgramRuleId"
                                   ORDER BY "UpdatedAt" DESC, "Id" DESC
                               ) AS rn
                        FROM "ProgramRuleCursors"
                        WHERE "StrandKey" IS NULL
                    )
                    WHERE rn > 1
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey",
                table: "ProgramRuleCursors",
                column: "ProgramRuleId",
                unique: true,
                filter: "[StrandKey] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey",
                table: "ProgramRuleCursors");
        }
    }
}
