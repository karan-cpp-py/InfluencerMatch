using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorRegistrationAndChannelLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatorProfiles",
                columns: table => new
                {
                    CreatorProfileId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    InstagramHandle = table.Column<string>(type: "text", nullable: true),
                    ContactEmail = table.Column<string>(type: "text", nullable: true),
                    Bio = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorProfiles", x => x.CreatorProfileId);
                    table.ForeignKey(
                        name: "FK_CreatorProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CollaborationRequests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BrandUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatorProfileId = table.Column<int>(type: "integer", nullable: false),
                    CampaignTitle = table.Column<string>(type: "text", nullable: false),
                    Budget = table.Column<decimal>(type: "numeric", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollaborationRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_CollaborationRequests_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "CreatorProfileId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CollaborationRequests_Users_BrandUserId",
                        column: x => x.BrandUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatorChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChannelId = table.Column<string>(type: "text", nullable: false),
                    CreatorProfileId = table.Column<int>(type: "integer", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ChannelUrl = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    Subscribers = table.Column<long>(type: "bigint", nullable: false),
                    TotalViews = table.Column<long>(type: "bigint", nullable: false),
                    VideoCount = table.Column<int>(type: "integer", nullable: false),
                    EngagementRate = table.Column<double>(type: "double precision", nullable: false),
                    CreatorTier = table.Column<string>(type: "text", nullable: true),
                    ChannelPublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    LastStatsUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorChannels", x => x.Id);
                    table.UniqueConstraint("AK_CreatorChannels_ChannelId", x => x.ChannelId);
                    table.ForeignKey(
                        name: "FK_CreatorChannels_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "CreatorProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YoutubeVideoId = table.Column<string>(type: "text", nullable: false),
                    ChannelId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    LikeCount = table.Column<long>(type: "bigint", nullable: false),
                    CommentCount = table.Column<long>(type: "bigint", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelVideos", x => x.Id);
                    table.UniqueConstraint("AK_ChannelVideos_YoutubeVideoId", x => x.YoutubeVideoId);
                    table.ForeignKey(
                        name: "FK_ChannelVideos_CreatorChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "CreatorChannels",
                        principalColumn: "ChannelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoMetrics",
                columns: table => new
                {
                    MetricId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YoutubeVideoId = table.Column<string>(type: "text", nullable: false),
                    Views = table.Column<long>(type: "bigint", nullable: false),
                    Likes = table.Column<long>(type: "bigint", nullable: false),
                    Comments = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoMetrics", x => x.MetricId);
                    table.ForeignKey(
                        name: "FK_VideoMetrics_ChannelVideos_YoutubeVideoId",
                        column: x => x.YoutubeVideoId,
                        principalTable: "ChannelVideos",
                        principalColumn: "YoutubeVideoId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelVideos_ChannelId",
                table: "ChannelVideos",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelVideos_PublishedAt",
                table: "ChannelVideos",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelVideos_YoutubeVideoId",
                table: "ChannelVideos",
                column: "YoutubeVideoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationRequests_BrandUserId",
                table: "CollaborationRequests",
                column: "BrandUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollaborationRequests_CreatorProfileId",
                table: "CollaborationRequests",
                column: "CreatorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorChannels_ChannelId",
                table: "CreatorChannels",
                column: "ChannelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorChannels_CreatorProfileId",
                table: "CreatorChannels",
                column: "CreatorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorChannels_CreatorTier",
                table: "CreatorChannels",
                column: "CreatorTier");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorChannels_EngagementRate",
                table: "CreatorChannels",
                column: "EngagementRate");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorChannels_Subscribers",
                table: "CreatorChannels",
                column: "Subscribers");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorProfiles_UserId",
                table: "CreatorProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoMetrics_YoutubeVideoId_RecordedAt",
                table: "VideoMetrics",
                columns: new[] { "YoutubeVideoId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollaborationRequests");

            migrationBuilder.DropTable(
                name: "VideoMetrics");

            migrationBuilder.DropTable(
                name: "ChannelVideos");

            migrationBuilder.DropTable(
                name: "CreatorChannels");

            migrationBuilder.DropTable(
                name: "CreatorProfiles");
        }
    }
}
