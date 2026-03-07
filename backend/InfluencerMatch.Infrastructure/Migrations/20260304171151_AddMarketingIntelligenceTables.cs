using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketingIntelligenceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrandMentions",
                columns: table => new
                {
                    BrandMentionId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VideoId = table.Column<string>(type: "text", nullable: false),
                    VideoTitle = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    BrandName = table.Column<string>(type: "text", nullable: false),
                    DetectionMethod = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandMentions", x => x.BrandMentionId);
                    table.ForeignKey(
                        name: "FK_BrandMentions_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorScores",
                columns: table => new
                {
                    ScoreId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    EngagementComponent = table.Column<double>(type: "double precision", nullable: false),
                    ViewsComponent = table.Column<double>(type: "double precision", nullable: false),
                    GrowthComponent = table.Column<double>(type: "double precision", nullable: false),
                    FrequencyComponent = table.Column<double>(type: "double precision", nullable: false),
                    EngagementRate = table.Column<double>(type: "double precision", nullable: false),
                    AverageViews = table.Column<double>(type: "double precision", nullable: false),
                    SubscriberGrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    UploadFrequency = table.Column<double>(type: "double precision", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorScores", x => x.ScoreId);
                    table.ForeignKey(
                        name: "FK_CreatorScores_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrandMentions_BrandName",
                table: "BrandMentions",
                column: "BrandName");

            migrationBuilder.CreateIndex(
                name: "IX_BrandMentions_CreatorId",
                table: "BrandMentions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandMentions_VideoId_BrandName",
                table: "BrandMentions",
                columns: new[] { "VideoId", "BrandName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorScores_CreatorId",
                table: "CreatorScores",
                column: "CreatorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorScores_Score",
                table: "CreatorScores",
                column: "Score");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrandMentions");

            migrationBuilder.DropTable(
                name: "CreatorScores");
        }
    }
}
