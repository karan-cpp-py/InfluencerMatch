using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCreatorRowsFromChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // For every registered CreatorChannel that does NOT already have a
            // matching Creator row, insert one so that all analytics pipelines
            // can process it immediately.
            migrationBuilder.Sql(@"
                INSERT INTO ""Creators"" (
                    ""UserId"",
                    ""ChannelId"",
                    ""ChannelName"",
                    ""Description"",
                    ""Subscribers"",
                    ""TotalViews"",
                    ""VideoCount"",
                    ""Category"",
                    ""Country"",
                    ""CreatorTier"",
                    ""IsSmallCreator"",
                    ""Platform"",
                    ""CreatedAt""
                )
                SELECT
                    cp.""UserId"",
                    cc.""ChannelId"",
                    cc.""ChannelName"",
                    cc.""Description"",
                    cc.""Subscribers"",
                    cc.""TotalViews"",
                    cc.""VideoCount"",
                    COALESCE(cp.""Category"", ''),
                    cp.""Country"",
                    cc.""CreatorTier"",
                    cc.""Subscribers"" <= 500000,
                    'YouTube',
                    NOW()
                FROM ""CreatorChannels"" cc
                JOIN ""CreatorProfiles"" cp ON cc.""CreatorProfileId"" = cp.""CreatorProfileId""
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""Creators"" c WHERE c.""ChannelId"" = cc.""ChannelId""
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove Creator rows that were backfilled (those whose ChannelId
            // matches a CreatorChannel entry but have no analytics data).
            migrationBuilder.Sql(@"
                DELETE FROM ""Creators""
                WHERE ""ChannelId"" IN (SELECT ""ChannelId"" FROM ""CreatorChannels"");
            ");
        }
    }
}
