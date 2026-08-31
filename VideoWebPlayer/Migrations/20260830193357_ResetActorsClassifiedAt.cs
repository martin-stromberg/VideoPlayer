using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoWebPlayer.Migrations
{
    /// <inheritdoc />
    public partial class ResetActorsClassifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Movies"" SET ""ActorsClassifiedAt"" = NULL WHERE ""IsManuallyEdited"" = 0;");
            migrationBuilder.Sql(@"UPDATE ""Movies"" SET ""ActorsClassifiedAt"" = '1900-01-01 00:00:00' WHERE ""IsManuallyEdited"" = 1 AND ""ActorsClassifiedAt"" IS NULL;");
            migrationBuilder.Sql(@"UPDATE ""TVShowEpisodes"" SET ""ActorsClassifiedAt"" = NULL WHERE ""IsManuallyEdited"" = 0;");
            migrationBuilder.Sql(@"UPDATE ""TVShowEpisodes"" SET ""ActorsClassifiedAt"" = '1900-01-01 00:00:00' WHERE ""IsManuallyEdited"" = 1 AND ""ActorsClassifiedAt"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
