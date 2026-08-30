using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchedEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchedEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    MovieId = table.Column<long>(type: "INTEGER", nullable: true),
                    TVShowEpisodeId = table.Column<long>(type: "INTEGER", nullable: true),
                    WatchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchedEntries", x => x.Id);
                    table.CheckConstraint("CK_WatchedEntries_ExactlyOneTitle", "((MovieId IS NOT NULL AND TVShowEpisodeId IS NULL) OR (MovieId IS NULL AND TVShowEpisodeId IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_WatchedEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchedEntries_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WatchedEntries_TVShowEpisodes_TVShowEpisodeId",
                        column: x => x.TVShowEpisodeId,
                        principalTable: "TVShowEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEntries_MovieId",
                table: "WatchedEntries",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEntries_TVShowEpisodeId",
                table: "WatchedEntries",
                column: "TVShowEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEntries_UserId",
                table: "WatchedEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEntries_UserId_MovieId",
                table: "WatchedEntries",
                columns: new[] { "UserId", "MovieId" },
                unique: true,
                filter: "[MovieId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WatchedEntries_UserId_TVShowEpisodeId",
                table: "WatchedEntries",
                columns: new[] { "UserId", "TVShowEpisodeId" },
                unique: true,
                filter: "[TVShowEpisodeId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WatchedEntries");
        }
    }
}
