using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeNest.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentVersionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Architecture",
                table: "Agents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "Agents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Architecture",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Agents");
        }
    }
}
