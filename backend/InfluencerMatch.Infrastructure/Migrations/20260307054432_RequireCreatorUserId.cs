using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RequireCreatorUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete all analytics data for creators that were auto-discovered
            // (legacy) and have no registered user. The cascade-delete FK
            // relationships on VideoAnalytics, CreatorAnalytics, BrandMentions,
            // CreatorScores, CreatorGrowthScores, CreatorGrowth, and Videos
            // ensure child rows are removed automatically.
            migrationBuilder.Sql(@"
                DELETE FROM ""Creators"" WHERE ""UserId"" IS NULL;
            ");

            // Now that no NULL rows remain, enforce NOT NULL on the column.
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Creators",
                type: "integer",
                nullable: false,
                defaultValue: 0,   // will never be inserted; all rows have a real UserId now
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Make the column nullable again
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Creators",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: false);
        }
    }
}
