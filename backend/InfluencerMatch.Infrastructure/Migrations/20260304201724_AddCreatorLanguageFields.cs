using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfluencerMatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorLanguageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LanguageConfidenceScore",
                table: "Creators",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Creators",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Creators_Language",
                table: "Creators",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_Creators_Region",
                table: "Creators",
                column: "Region");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Creators_Language",
                table: "Creators");

            migrationBuilder.DropIndex(
                name: "IX_Creators_Region",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "LanguageConfidenceScore",
                table: "Creators");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Creators");
        }
    }
}
