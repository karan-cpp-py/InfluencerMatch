using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCreatorUserIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Previous migration RequireCreatorUserId forced NOT NULL + default 0.
            // Importer-created creators require nullable UserId (imported/non-registered creators).
            migrationBuilder.Sql(@"
                ALTER TABLE ""Creators"" ALTER COLUMN ""UserId"" DROP NOT NULL;
                ALTER TABLE ""Creators"" ALTER COLUMN ""UserId"" DROP DEFAULT;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Creators"" SET ""UserId"" = 0 WHERE ""UserId"" IS NULL;
                ALTER TABLE ""Creators"" ALTER COLUMN ""UserId"" SET DEFAULT 0;
                ALTER TABLE ""Creators"" ALTER COLUMN ""UserId"" SET NOT NULL;
            ");
        }
    }
}
