using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistBackfillMarkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PlaylistBackfillLastSweepAt",
                table: "Setups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlaylistBackfillMarkers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaType = table.Column<string>(type: "TEXT", nullable: false),
                    MediaId = table.Column<long>(type: "INTEGER", nullable: false),
                    MarkedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistBackfillMarkers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistEntries_MediaType_MediaId",
                table: "PlaylistEntries",
                columns: new[] { "MediaType", "MediaId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistBackfillMarkers_MediaType_MediaId",
                table: "PlaylistBackfillMarkers",
                columns: new[] { "MediaType", "MediaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaylistBackfillMarkers");

            migrationBuilder.DropIndex(
                name: "IX_PlaylistEntries_MediaType_MediaId",
                table: "PlaylistEntries");

            migrationBuilder.DropColumn(
                name: "PlaylistBackfillLastSweepAt",
                table: "Setups");
        }
    }
}
