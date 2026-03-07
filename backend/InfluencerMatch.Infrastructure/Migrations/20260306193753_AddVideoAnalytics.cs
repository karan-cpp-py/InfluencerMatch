using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoAnalytics",
                columns: table => new
                {
                    VideoAnalyticsId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YoutubeVideoId = table.Column<string>(type: "text", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Views = table.Column<long>(type: "bigint", nullable: false),
                    Likes = table.Column<long>(type: "bigint", nullable: false),
                    Comments = table.Column<long>(type: "bigint", nullable: false),
                    EngagementRate = table.Column<double>(type: "double precision", nullable: false),
                    BrandName = table.Column<string>(type: "text", nullable: true),
                    VideoType = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoAnalytics", x => x.VideoAnalyticsId);
                    table.ForeignKey(
                        name: "FK_VideoAnalytics_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_BrandName",
                table: "VideoAnalytics",
                column: "BrandName");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_CreatorId",
                table: "VideoAnalytics",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_VideoType",
                table: "VideoAnalytics",
                column: "VideoType");

            migrationBuilder.CreateIndex(
                name: "IX_VideoAnalytics_YoutubeVideoId",
                table: "VideoAnalytics",
                column: "YoutubeVideoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoAnalytics");
        }
    }
}
