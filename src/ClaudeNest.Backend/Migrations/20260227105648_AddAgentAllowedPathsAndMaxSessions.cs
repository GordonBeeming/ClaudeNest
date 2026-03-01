using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeNest.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentAllowedPathsAndMaxSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedPathsJson",
                table: "Agents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxSessions",
                table: "Agents",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedPathsJson",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "MaxSessions",
                table: "Agents");
        }
    }
}
