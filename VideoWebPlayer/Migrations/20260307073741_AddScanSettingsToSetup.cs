using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddScanSettingsToSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MediaCollectionScanIntervalDays",
                table: "Setups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "ScanProcessIntervalMinutes",
                table: "Setups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaCollectionScanIntervalDays",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "ScanProcessIntervalMinutes",
                table: "Setups");
        }
    }
}
