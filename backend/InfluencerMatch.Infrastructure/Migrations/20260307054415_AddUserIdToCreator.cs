using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Creators",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Creators_UserId",
                table: "Creators",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Creators_Users_UserId",
                table: "Creators",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Creators_Users_UserId",
                table: "Creators");

            migrationBuilder.DropIndex(
                name: "IX_Creators_UserId",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Creators");
        }
    }
}
