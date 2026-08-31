using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class MakePictureMediaItemIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pictures_MediaItems_MediaItemId",
                table: "Pictures");

            migrationBuilder.AlterColumn<long>(
                name: "MediaItemId",
                table: "Pictures",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_Pictures_MediaItems_MediaItemId",
                table: "Pictures",
                column: "MediaItemId",
                principalTable: "MediaItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pictures_MediaItems_MediaItemId",
                table: "Pictures");

            migrationBuilder.AlterColumn<long>(
                name: "MediaItemId",
                table: "Pictures",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Pictures_MediaItems_MediaItemId",
                table: "Pictures",
                column: "MediaItemId",
                principalTable: "MediaItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
