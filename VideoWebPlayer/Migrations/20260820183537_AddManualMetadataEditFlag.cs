using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddManualMetadataEditFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "TVShowSeasons",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "TVShows",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "TVShowEpisodes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "Movies",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyEdited",
                table: "MovieCollections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "TVShowSeasons");

            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "TVShows");

            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "TVShowEpisodes");

            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "IsManuallyEdited",
                table: "MovieCollections");
        }
    }
}
