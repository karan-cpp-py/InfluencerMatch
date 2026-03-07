using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseLeadActivityTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnterpriseLeadActivities",
                columns: table => new
                {
                    EnterpriseLeadActivityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnterpriseLeadId = table.Column<int>(type: "integer", nullable: false),
                    ActivityType = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnterpriseLeadActivities", x => x.EnterpriseLeadActivityId);
                    table.ForeignKey(
                        name: "FK_EnterpriseLeadActivities_EnterpriseLeads_EnterpriseLeadId",
                        column: x => x.EnterpriseLeadId,
                        principalTable: "EnterpriseLeads",
                        principalColumn: "EnterpriseLeadId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EnterpriseLeadActivities_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnterpriseLeadActivities_ActorUserId",
                table: "EnterpriseLeadActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EnterpriseLeadActivities_EnterpriseLeadId_CreatedAt",
                table: "EnterpriseLeadActivities",
                columns: new[] { "EnterpriseLeadId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnterpriseLeadActivities");
        }
    }
}
