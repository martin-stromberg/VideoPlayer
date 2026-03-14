using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSourceIconUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "IconPictureId",
                table: "MediaSources",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaSourceIcons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaSourceIcons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaSources_IconPictureId",
                table: "MediaSources",
                column: "IconPictureId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaSources_MediaSourceIcons_IconPictureId",
                table: "MediaSources",
                column: "IconPictureId",
                principalTable: "MediaSourceIcons",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaSources_MediaSourceIcons_IconPictureId",
                table: "MediaSources");

            migrationBuilder.DropTable(
                name: "MediaSourceIcons");

            migrationBuilder.DropIndex(
                name: "IX_MediaSources_IconPictureId",
                table: "MediaSources");

            migrationBuilder.DropColumn(
                name: "IconPictureId",
                table: "MediaSources");
        }
    }
}
