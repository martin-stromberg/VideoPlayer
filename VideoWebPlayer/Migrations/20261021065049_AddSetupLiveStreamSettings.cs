using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSetupLiveStreamSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LiveStreamSegmentDirectory",
                table: "Setups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LiveStreamSegmentLengthSeconds",
                table: "Setups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LiveStreamSegmentRetentionMinutes",
                table: "Setups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LiveStreamSegmentDirectory",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "LiveStreamSegmentLengthSeconds",
                table: "Setups");

            migrationBuilder.DropColumn(
                name: "LiveStreamSegmentRetentionMinutes",
                table: "Setups");
        }
    }
}
