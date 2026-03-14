using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMediaModels3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Changed",
                table: "MediaSources",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClassifiedAt",
                table: "MediaSources",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MovieId",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TVShowEpisodeId",
                table: "MediaItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Changed",
                table: "MediaCollections",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClassifiedAt",
                table: "MediaCollections",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MovieCollections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MediaSourceId = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectionId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Changed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TVShows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalName = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    Plot = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: true),
                    Studio = table.Column<string>(type: "TEXT", nullable: true),
                    Genres = table.Column<string>(type: "TEXT", nullable: true),
                    Premiered = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MediaSourceId = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectionId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Changed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TVShows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MovieCollectionId = table.Column<long>(type: "INTEGER", nullable: true),
                    OriginalTitle = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PremieredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Country = table.Column<string>(type: "TEXT", nullable: true),
                    Genres = table.Column<string>(type: "TEXT", nullable: true),
                    Studios = table.Column<string>(type: "TEXT", nullable: true),
                    Director = table.Column<string>(type: "TEXT", nullable: true),
                    Credits = table.Column<string>(type: "TEXT", nullable: true),
                    Plot = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MediaSourceId = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectionId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Changed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movies_MovieCollections_MovieCollectionId",
                        column: x => x.MovieCollectionId,
                        principalTable: "MovieCollections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TVShowSeasons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TVShowId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MediaSourceId = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectionId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Changed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TVShowSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TVShowSeasons_TVShows_TVShowId",
                        column: x => x.TVShowId,
                        principalTable: "TVShows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovieMediaItems",
                columns: table => new
                {
                    MovieId = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    MovieId1 = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieMediaItems", x => new { x.MovieId, x.MediaItemId });
                    table.ForeignKey(
                        name: "FK_MovieMediaItems_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovieMediaItems_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovieMediaItems_Movies_MovieId1",
                        column: x => x.MovieId1,
                        principalTable: "Movies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TVShowEpisodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    TVShowSeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PremieredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Plot = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MediaSourceId = table.Column<long>(type: "INTEGER", nullable: false),
                    CollectionId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClassifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Changed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TVShowEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TVShowEpisodes_TVShowSeasons_TVShowSeasonId",
                        column: x => x.TVShowSeasonId,
                        principalTable: "TVShowSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TVShowEpisodeMediaItems",
                columns: table => new
                {
                    TVShowEpisodeId = table.Column<long>(type: "INTEGER", nullable: false),
                    MediaItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    TVShowEpisodeId1 = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TVShowEpisodeMediaItems", x => new { x.TVShowEpisodeId, x.MediaItemId });
                    table.ForeignKey(
                        name: "FK_TVShowEpisodeMediaItems_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TVShowEpisodeMediaItems_TVShowEpisodes_TVShowEpisodeId",
                        column: x => x.TVShowEpisodeId,
                        principalTable: "TVShowEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TVShowEpisodeMediaItems_TVShowEpisodes_TVShowEpisodeId1",
                        column: x => x.TVShowEpisodeId1,
                        principalTable: "TVShowEpisodes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_MovieId",
                table: "MediaItems",
                column: "MovieId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_TVShowEpisodeId",
                table: "MediaItems",
                column: "TVShowEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieMediaItems_MediaItemId",
                table: "MovieMediaItems",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieMediaItems_MovieId1",
                table: "MovieMediaItems",
                column: "MovieId1");

            migrationBuilder.CreateIndex(
                name: "IX_Movies_MovieCollectionId",
                table: "Movies",
                column: "MovieCollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodeMediaItems_MediaItemId",
                table: "TVShowEpisodeMediaItems",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodeMediaItems_TVShowEpisodeId1",
                table: "TVShowEpisodeMediaItems",
                column: "TVShowEpisodeId1");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowEpisodes_TVShowSeasonId",
                table: "TVShowEpisodes",
                column: "TVShowSeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_TVShowSeasons_TVShowId",
                table: "TVShowSeasons",
                column: "TVShowId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_Movies_MovieId",
                table: "MediaItems",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_TVShowEpisodes_TVShowEpisodeId",
                table: "MediaItems",
                column: "TVShowEpisodeId",
                principalTable: "TVShowEpisodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_Movies_MovieId",
                table: "MediaItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_TVShowEpisodes_TVShowEpisodeId",
                table: "MediaItems");

            migrationBuilder.DropTable(
                name: "MovieMediaItems");

            migrationBuilder.DropTable(
                name: "TVShowEpisodeMediaItems");

            migrationBuilder.DropTable(
                name: "Movies");

            migrationBuilder.DropTable(
                name: "TVShowEpisodes");

            migrationBuilder.DropTable(
                name: "MovieCollections");

            migrationBuilder.DropTable(
                name: "TVShowSeasons");

            migrationBuilder.DropTable(
                name: "TVShows");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_MovieId",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_TVShowEpisodeId",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Changed",
                table: "MediaSources");

            migrationBuilder.DropColumn(
                name: "ClassifiedAt",
                table: "MediaSources");

            migrationBuilder.DropColumn(
                name: "MovieId",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "TVShowEpisodeId",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Changed",
                table: "MediaCollections");

            migrationBuilder.DropColumn(
                name: "ClassifiedAt",
                table: "MediaCollections");
        }
    }
}
