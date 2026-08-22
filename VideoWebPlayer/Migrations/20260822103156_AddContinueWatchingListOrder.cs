using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddContinueWatchingListOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ListOrder",
                table: "ContinueWatchingEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE ContinueWatchingEntries
                SET ListOrder = CAST((julianday(UpdatedAt) - 1721425.5) * 864000000000 AS INTEGER)
                WHERE UpdatedAt IS NOT NULL
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_ListOrder_UpdatedAt",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "ListOrder", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContinueWatchingEntries_UserId_ListOrder_UpdatedAt",
                table: "ContinueWatchingEntries");

            migrationBuilder.DropColumn(
                name: "ListOrder",
                table: "ContinueWatchingEntries");
        }
    }
}
