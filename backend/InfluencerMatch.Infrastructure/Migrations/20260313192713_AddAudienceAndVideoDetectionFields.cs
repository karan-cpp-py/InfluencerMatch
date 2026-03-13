using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudienceAndVideoDetectionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DetectionConfidence",
                table: "VideoAnalytics",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "VideoAnalytics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AudienceDemographicsFetchedAt",
                table: "CreatorProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudienceDemographicsJson",
                table: "CreatorProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetectionConfidence",
                table: "VideoAnalytics");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "VideoAnalytics");

            migrationBuilder.DropColumn(
                name: "AudienceDemographicsFetchedAt",
                table: "CreatorProfiles");

            migrationBuilder.DropColumn(
                name: "AudienceDemographicsJson",
                table: "CreatorProfiles");
        }
    }
}
