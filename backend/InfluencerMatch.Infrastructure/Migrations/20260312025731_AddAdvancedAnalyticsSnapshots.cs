using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedAnalyticsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignAnalyticsSnapshots",
                columns: table => new
                {
                    CampaignAnalyticsSnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    Reach = table.Column<long>(type: "bigint", nullable: false),
                    EngagedViews = table.Column<double>(type: "double precision", nullable: false),
                    EngagementRate = table.Column<double>(type: "double precision", nullable: false),
                    Cpm = table.Column<double>(type: "double precision", nullable: false),
                    Cpe = table.Column<double>(type: "double precision", nullable: false),
                    CpcLikeProxy = table.Column<double>(type: "double precision", nullable: false),
                    OverperformerCount = table.Column<int>(type: "integer", nullable: false),
                    UnderperformerCount = table.Column<int>(type: "integer", nullable: false),
                    CreatorContributionsJson = table.Column<string>(type: "text", nullable: false),
                    EstimatedViewsLow = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedViewsExpected = table.Column<double>(type: "double precision", nullable: false),
                    EstimatedViewsHigh = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedEngagementLow = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedEngagementExpected = table.Column<double>(type: "double precision", nullable: false),
                    ExpectedEngagementHigh = table.Column<double>(type: "double precision", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    ConfidenceTier = table.Column<string>(type: "text", nullable: false),
                    BudgetScenariosJson = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAnalyticsSnapshots", x => x.CampaignAnalyticsSnapshotId);
                    table.ForeignKey(
                        name: "FK_CampaignAnalyticsSnapshots_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "CampaignId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorHealthSnapshots",
                columns: table => new
                {
                    CreatorHealthSnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    CompositeScore = table.Column<double>(type: "double precision", nullable: false),
                    ConsistencyScore = table.Column<double>(type: "double precision", nullable: false),
                    EngagementQualityScore = table.Column<double>(type: "double precision", nullable: false),
                    GrowthStabilityScore = table.Column<double>(type: "double precision", nullable: false),
                    ContentRelevanceScore = table.Column<double>(type: "double precision", nullable: false),
                    BrandSafetyScore = table.Column<double>(type: "double precision", nullable: false),
                    Trend7d = table.Column<string>(type: "text", nullable: false),
                    Trend30d = table.Column<string>(type: "text", nullable: false),
                    Delta7dPercent = table.Column<double>(type: "double precision", nullable: false),
                    Delta30dPercent = table.Column<double>(type: "double precision", nullable: false),
                    SuspiciousEngagementRatio = table.Column<double>(type: "double precision", nullable: false),
                    LikeCommentViewConsistencyScore = table.Column<double>(type: "double precision", nullable: false),
                    EngagementVolatilityFlag = table.Column<bool>(type: "boolean", nullable: false),
                    EngagementVolatilityScore = table.Column<double>(type: "double precision", nullable: false),
                    ReusedCommentPatternScore = table.Column<double>(type: "double precision", nullable: false),
                    WhyExplanation = table.Column<string>(type: "text", nullable: false),
                    AudienceExplanation = table.Column<string>(type: "text", nullable: false),
                    BestPostingWindow = table.Column<string>(type: "text", nullable: false),
                    ContentFormatPerformanceJson = table.Column<string>(type: "text", nullable: false),
                    RetentionSuggestionsJson = table.Column<string>(type: "text", nullable: false),
                    WeeklyActionsJson = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorHealthSnapshots", x => x.CreatorHealthSnapshotId);
                    table.ForeignKey(
                        name: "FK_CreatorHealthSnapshots_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAnalyticsSnapshots_CalculatedAt",
                table: "CampaignAnalyticsSnapshots",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAnalyticsSnapshots_CampaignId",
                table: "CampaignAnalyticsSnapshots",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorHealthSnapshots_CalculatedAt",
                table: "CreatorHealthSnapshots",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorHealthSnapshots_CreatorId",
                table: "CreatorHealthSnapshots",
                column: "CreatorId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignAnalyticsSnapshots");

            migrationBuilder.DropTable(
                name: "CreatorHealthSnapshots");
        }
    }
}
