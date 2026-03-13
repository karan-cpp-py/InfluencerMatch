using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorYouTubeAnalyticsConnectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "YouTubeAnalyticsConnectedAt",
                table: "CreatorProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YouTubeAnalyticsRefreshToken",
                table: "CreatorProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YouTubeAnalyticsConnectedAt",
                table: "CreatorProfiles");

            migrationBuilder.DropColumn(
                name: "YouTubeAnalyticsRefreshToken",
                table: "CreatorProfiles");
        }
    }
}
