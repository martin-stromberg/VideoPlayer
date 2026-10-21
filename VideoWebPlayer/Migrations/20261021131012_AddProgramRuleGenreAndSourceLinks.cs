using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramRuleGenreAndSourceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgramRuleGenres",
                columns: table => new
                {
                    ProgramRuleId = table.Column<long>(type: "INTEGER", nullable: false),
                    GenreId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramRuleGenres", x => new { x.ProgramRuleId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_ProgramRuleGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramRuleGenres_ProgramRules_ProgramRuleId",
                        column: x => x.ProgramRuleId,
                        principalTable: "ProgramRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProgramRuleMediaSources",
                columns: table => new
                {
                    ProgramRuleId = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaSourceId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramRuleMediaSources", x => new { x.ProgramRuleId, x.MediaSourceId });
                    table.ForeignKey(
                        name: "FK_ProgramRuleMediaSources_MediaSources_MediaSourceId",
                        column: x => x.MediaSourceId,
                        principalTable: "MediaSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramRuleMediaSources_ProgramRules_ProgramRuleId",
                        column: x => x.ProgramRuleId,
                        principalTable: "ProgramRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRuleGenres_GenreId",
                table: "ProgramRuleGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramRuleMediaSources_MediaSourceId",
                table: "ProgramRuleMediaSources",
                column: "MediaSourceId");

            // Backfill: existing genre rules carried their single genre in ProgramRules.MaterialId —
            // mirror it into the new join table so existing rules keep working unchanged.
            migrationBuilder.Sql(
                "INSERT INTO ProgramRuleGenres (ProgramRuleId, GenreId) " +
                "SELECT Id, MaterialId FROM ProgramRules " +
                "WHERE MaterialType = 3 AND MaterialId IN (SELECT Id FROM Genres)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramRuleGenres");

            migrationBuilder.DropTable(
                name: "ProgramRuleMediaSources");
        }
    }
}
