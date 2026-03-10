using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnrichCreatorAndVideoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EngagementRate",
                table: "Videos",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "AvgComments",
                table: "Creators",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AvgLikes",
                table: "Creators",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AvgViews",
                table: "Creators",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelTags",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelUrl",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EngagementRate",
                table: "Creators",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "InstagramHandle",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRefreshedAt",
                table: "Creators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicEmail",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Creators",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailUrl",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwitterHandle",
                table: "Creators",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "EngagementRate",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "AvgComments",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "AvgLikes",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "AvgViews",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "ChannelTags",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "ChannelUrl",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "EngagementRate",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "InstagramHandle",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "LastRefreshedAt",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "PublicEmail",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "ThumbnailUrl",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "TwitterHandle",
                table: "Creators");
        }
    }
}
