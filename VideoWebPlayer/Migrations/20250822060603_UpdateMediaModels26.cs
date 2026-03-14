using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedLoginIps",
                columns: table => new
                {
                    Ip = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BlockedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Failures = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedLoginIps", x => x.Ip);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedLoginIps_BlockedAtUtc",
                table: "BlockedLoginIps",
                column: "BlockedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedLoginIps");
        }
    }
}
