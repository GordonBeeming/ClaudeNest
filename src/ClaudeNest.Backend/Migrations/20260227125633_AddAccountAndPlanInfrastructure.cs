using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClaudeNest.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountAndPlanInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_Users_UserId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "MaxSessions",
                table: "Agents");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Agents",
                newName: "AccountId");

            migrationBuilder.RenameIndex(
                name: "IX_Agents_UserId",
                table: "Agents",
                newName: "IX_Agents_AccountId");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaxAgents = table.Column<int>(type: "int", nullable: false),
                    MaxSessions = table.Column<int>(type: "int", nullable: false),
                    PriceCents = table.Column<int>(type: "int", nullable: false),
                    TrialDays = table.Column<int>(type: "int", nullable: false),
                    StripeProductId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubscriptionStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TrialEndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accounts_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Plans",
                columns: new[] { "Id", "IsActive", "MaxAgents", "MaxSessions", "Name", "PriceCents", "SortOrder", "StripeProductId", "TrialDays" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), true, 1, 2, "Wren", 100, 1, null, 14 },
                    { new Guid("10000000-0000-0000-0000-000000000002"), true, 2, 5, "Robin", 200, 2, null, 0 },
                    { new Guid("10000000-0000-0000-0000-000000000003"), true, 3, 10, "Hawk", 500, 3, null, 0 },
                    { new Guid("10000000-0000-0000-0000-000000000004"), true, 5, 25, "Eagle", 1000, 4, null, 0 },
                    { new Guid("10000000-0000-0000-0000-000000000005"), true, 10, 50, "Falcon", 2000, 5, null, 0 },
                    { new Guid("10000000-0000-0000-0000-000000000006"), true, 25, 100, "Condor", 5000, 6, null, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_AccountId",
                table: "Users",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_PlanId",
                table: "Accounts",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Accounts_AccountId",
                table: "Agents",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Accounts_AccountId",
                table: "Users",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_Accounts_AccountId",
                table: "Agents");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Accounts_AccountId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Users_AccountId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "Agents",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Agents_AccountId",
                table: "Agents",
                newName: "IX_Agents_UserId");

            migrationBuilder.AddColumn<int>(
                name: "MaxSessions",
                table: "Agents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Users_UserId",
                table: "Agents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
