using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDifferentiatorAnalyticsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandSovSnapshots",
                columns: table => new
                {
                    BrandSovSnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BrandName = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    CompetitorSovJson = table.Column<string>(type: "text", nullable: false),
                    RegionalLanguagePerformanceJson = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandSovSnapshots", x => x.BrandSovSnapshotId);
                });

            migrationBuilder.CreateTable(
                name: "CampaignStrategySnapshots",
                columns: table => new
                {
                    CampaignStrategySnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    NegotiationIntelligenceJson = table.Column<string>(type: "text", nullable: false),
                    CreativeBriefIntelligenceJson = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignStrategySnapshots", x => x.CampaignStrategySnapshotId);
                    table.ForeignKey(
                        name: "FK_CampaignStrategySnapshots_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "CampaignId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorReadinessSnapshots",
                columns: table => new
                {
                    CreatorReadinessSnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    SponsorshipReadinessIndex = table.Column<double>(type: "double precision", nullable: false),
                    ReliabilityScore = table.Column<double>(type: "double precision", nullable: false),
                    ContentHygieneScore = table.Column<double>(type: "double precision", nullable: false),
                    BrandFitStabilityScore = table.Column<double>(type: "double precision", nullable: false),
                    ConversionProxyScore = table.Column<double>(type: "double precision", nullable: false),
                    ReadinessRoadmapJson = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorReadinessSnapshots", x => x.CreatorReadinessSnapshotId);
                    table.ForeignKey(
                        name: "FK_CreatorReadinessSnapshots_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpportunityRadarSnapshots",
                columns: table => new
                {
                    OpportunityRadarSnapshotId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    RisingCreatorsJson = table.Column<string>(type: "text", nullable: false),
                    CategoryTrendsJson = table.Column<string>(type: "text", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpportunityRadarSnapshots", x => x.OpportunityRadarSnapshotId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrandSovSnapshots_BrandName_Category_Country_Language",
                table: "BrandSovSnapshots",
                columns: new[] { "BrandName", "Category", "Country", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandSovSnapshots_CalculatedAt",
                table: "BrandSovSnapshots",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStrategySnapshots_CalculatedAt",
                table: "CampaignStrategySnapshots",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStrategySnapshots_CampaignId",
                table: "CampaignStrategySnapshots",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorReadinessSnapshots_CalculatedAt",
                table: "CreatorReadinessSnapshots",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorReadinessSnapshots_CreatorId",
                table: "CreatorReadinessSnapshots",
                column: "CreatorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityRadarSnapshots_CalculatedAt",
                table: "OpportunityRadarSnapshots",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityRadarSnapshots_Category_Country_Language",
                table: "OpportunityRadarSnapshots",
                columns: new[] { "Category", "Country", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandSovSnapshots");

            migrationBuilder.DropTable(
                name: "CampaignStrategySnapshots");

            migrationBuilder.DropTable(
                name: "CreatorReadinessSnapshots");

            migrationBuilder.DropTable(
                name: "OpportunityRadarSnapshots");
        }
    }
}
