using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdateSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UpdateSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AutomaticChecksEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CheckIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowPrereleaseUpdates = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutomaticInstallationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutomaticDownloadEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ServiceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreateBackupBeforeInstallation = table.Column<bool>(type: "INTEGER", nullable: false),
                    CancelInstallationOnBackupFailure = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdateBackupPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    RetainedUpdateBackupCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpdateSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UpdateSettings");
        }
    }
}
