using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorGrowthScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatorGrowthScores",
                columns: table => new
                {
                    CreatorGrowthScoreId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    GrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    GrowthCategory = table.Column<string>(type: "text", nullable: false),
                    SubscriberDelta = table.Column<long>(type: "bigint", nullable: false),
                    BaselineSubscribers = table.Column<long>(type: "bigint", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorGrowthScores", x => x.CreatorGrowthScoreId);
                    table.ForeignKey(
                        name: "FK_CreatorGrowthScores_Creators_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Creators",
                        principalColumn: "CreatorId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorGrowthScores_CreatorId",
                table: "CreatorGrowthScores",
                column: "CreatorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorGrowthScores_GrowthRate",
                table: "CreatorGrowthScores",
                column: "GrowthRate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatorGrowthScores");
        }
    }
}
