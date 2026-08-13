using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupOperationHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Generation = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Succeeded = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupOperationHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackupSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StoragePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    AutomaticBackupsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SonRetentionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FatherRetentionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    GrandfatherRetentionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxUploadSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupOperationHistories");

            migrationBuilder.DropTable(
                name: "BackupSettings");
        }
    }
}
