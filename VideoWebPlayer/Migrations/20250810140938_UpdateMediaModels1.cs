using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaCollections_MediaSources_MediaSourceId",
                table: "MediaCollections");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_MediaCollections_MediaCollectionId",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_MediaCollectionId",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaCollections_MediaSourceId",
                table: "MediaCollections");

            migrationBuilder.AddColumn<bool>(
                name: "Changed",
                table: "MediaItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MediaCollectionId1",
                table: "MediaItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MediaSourceId1",
                table: "MediaCollections",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_MediaCollectionId1",
                table: "MediaItems",
                column: "MediaCollectionId1");

            migrationBuilder.CreateIndex(
                name: "IX_MediaCollections_MediaSourceId1",
                table: "MediaCollections",
                column: "MediaSourceId1");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaCollections_MediaSources_MediaSourceId1",
                table: "MediaCollections",
                column: "MediaSourceId1",
                principalTable: "MediaSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_MediaCollections_MediaCollectionId1",
                table: "MediaItems",
                column: "MediaCollectionId1",
                principalTable: "MediaCollections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaCollections_MediaSources_MediaSourceId1",
                table: "MediaCollections");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_MediaCollections_MediaCollectionId1",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_MediaCollectionId1",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaCollections_MediaSourceId1",
                table: "MediaCollections");

            migrationBuilder.DropColumn(
                name: "Changed",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "MediaCollectionId1",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "MediaSourceId1",
                table: "MediaCollections");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_MediaCollectionId",
                table: "MediaItems",
                column: "MediaCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaCollections_MediaSourceId",
                table: "MediaCollections",
                column: "MediaSourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaCollections_MediaSources_MediaSourceId",
                table: "MediaCollections",
                column: "MediaSourceId",
                principalTable: "MediaSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_MediaCollections_MediaCollectionId",
                table: "MediaItems",
                column: "MediaCollectionId",
                principalTable: "MediaCollections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
