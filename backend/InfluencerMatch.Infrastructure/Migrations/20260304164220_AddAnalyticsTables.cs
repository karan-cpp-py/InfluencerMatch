using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatorAnalytics",
                columns: table => new
                {
                    CreatorAnalyticsId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    AvgViews = table.Column<double>(type: "double precision", nullable: false),
                    AvgLikes = table.Column<double>(type: "double precision", nullable: false),
                    AvgComments = table.Column<double>(type: "double precision", nullable: false),
                    EngagementRate = table.Column<double>(type: "double precision", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorAnalytics", x => x.CreatorAnalyticsId);
                    table.ForeignKey(
                        name: "FK_CreatorAnalytics_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorGrowth",
                columns: table => new
                {
                    GrowthId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Subscribers = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorGrowth", x => x.GrowthId);
                    table.ForeignKey(
                        name: "FK_CreatorGrowth_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorAnalytics_CreatorId",
                table: "CreatorAnalytics",
                column: "CreatorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorGrowth_CreatorId_RecordedAt",
                table: "CreatorGrowth",
                columns: new[] { "CreatorId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatorAnalytics");

            migrationBuilder.DropTable(
                name: "CreatorGrowth");
        }
    }
}
