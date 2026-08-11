using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddPictureGeneratedBackgroundProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EpisodeIdReference",
                table: "Pictures",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGeneratedBackground",
                table: "Pictures",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Pictures_EpisodeIdReference_IsGeneratedBackground",
                table: "Pictures",
                columns: new[] { "EpisodeIdReference", "IsGeneratedBackground" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pictures_EpisodeIdReference_IsGeneratedBackground",
                table: "Pictures");

            migrationBuilder.DropColumn(
                name: "EpisodeIdReference",
                table: "Pictures");

            migrationBuilder.DropColumn(
                name: "IsGeneratedBackground",
                table: "Pictures");
        }
    }
}
