using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BannerPictureId",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PosterPictureId",
                table: "Movies",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Pictures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pictures_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Setups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DataVersion = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Setups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Movies_BannerPictureId",
                table: "Movies",
                column: "BannerPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_PosterPictureId",
                table: "Movies",
                column: "PosterPictureId");

            migrationBuilder.CreateIndex(
                name: "IX_Pictures_MediaItemId",
                table: "Pictures",
                column: "MediaItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Pictures_BannerPictureId",
                table: "Movies",
                column: "BannerPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Pictures_PosterPictureId",
                table: "Movies",
                column: "PosterPictureId",
                principalTable: "Pictures",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Pictures_BannerPictureId",
                table: "Movies");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Pictures_PosterPictureId",
                table: "Movies");

            migrationBuilder.DropTable(
                name: "Pictures");

            migrationBuilder.DropTable(
                name: "Setups");

            migrationBuilder.DropIndex(
                name: "IX_Movies_BannerPictureId",
                table: "Movies");

            migrationBuilder.DropIndex(
                name: "IX_Movies_PosterPictureId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "BannerPictureId",
                table: "Movies");

            migrationBuilder.DropColumn(
                name: "PosterPictureId",
                table: "Movies");
        }
    }
}
