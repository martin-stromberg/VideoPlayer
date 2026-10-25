using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSetupsSingleRowIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data migration first: the Setups table is a singleton, but check-then-act creation
            // (data upgrade racing the first request) could leave a second row behind — keep the
            // oldest row, the same one the ordered read paths return (Issue #279).
            migrationBuilder.Sql(
                """
                DELETE FROM "Setups"
                WHERE "Id" NOT IN (SELECT MIN("Id") FROM "Setups");
                """);

            // Unique index over the constant expression "Id IS NOT NULL": every row maps to the
            // same key, so the table can never hold more than one row — regardless of which
            // code path inserts it (SQLite does not allow literal constants in index expressions,
            // hence the column reference).
            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX "UX_Setups_SingleRow"
                ON "Setups" (("Id" IS NOT NULL));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX "UX_Setups_SingleRow";
                """);
        }
    }
}
