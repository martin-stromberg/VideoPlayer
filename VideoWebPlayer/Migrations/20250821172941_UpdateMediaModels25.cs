using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContinueWatchingEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    MovieId = table.Column<long>(type: "INTEGER", nullable: true),
                    TVShowEpisodeId = table.Column<long>(type: "INTEGER", nullable: true),
                    Position = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContinueWatchingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContinueWatchingEntries_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContinueWatchingEntries_TVShowEpisodes_TVShowEpisodeId",
                        column: x => x.TVShowEpisodeId,
                        principalTable: "TVShowEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_MovieId",
                table: "ContinueWatchingEntries",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_TVShowEpisodeId",
                table: "ContinueWatchingEntries",
                column: "TVShowEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_MovieId",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "MovieId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContinueWatchingEntries_UserId_TVShowEpisodeId",
                table: "ContinueWatchingEntries",
                columns: new[] { "UserId", "TVShowEpisodeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContinueWatchingEntries");
        }
    }
}
