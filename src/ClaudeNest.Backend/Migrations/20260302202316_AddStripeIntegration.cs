using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeNest.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentCredentials_Agents_AgentId",
                table: "AgentCredentials");

            migrationBuilder.DropForeignKey(
                name: "FK_Agents_Accounts_AccountId",
                table: "Agents");

            migrationBuilder.DropForeignKey(
                name: "FK_PairingTokens_Users_UserId",
                table: "PairingTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Agents_AgentId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Accounts_AccountId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TrialDays",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Accounts");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultCouponId",
                table: "Plans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePriceId",
                table: "Plans",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripePaymentMethodFingerprint",
                table: "Accounts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyDeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Domain = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDeals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyDeals_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompanyDeals_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FreeMonths = table.Column<int>(type: "int", nullable: false),
                    MaxRedemptions = table.Column<int>(type: "int", nullable: false),
                    TimesRedeemed = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StripeCouponId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Coupons_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Coupons_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AmountCents = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false, defaultValue: "aud"),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StripeInvoiceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StripePaymentIntentId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompanyDealId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLedger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountLedger_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountLedger_CompanyDeals_CompanyDealId",
                        column: x => x.CompanyDealId,
                        principalTable: "CompanyDeals",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountLedger_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountLedger_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CouponRedemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    CouponId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FreeUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StripeCheckoutSessionId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CouponRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CouponRedemptions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CouponRedemptions_Coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupons",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                columns: new[] { "DefaultCouponId", "StripePriceId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                columns: new[] { "DefaultCouponId", "StripePriceId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                columns: new[] { "DefaultCouponId", "StripePriceId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                columns: new[] { "DefaultCouponId", "StripePriceId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                columns: new[] { "DefaultCouponId", "StripePriceId" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "DefaultCouponId", "StripePriceId" },
                values: new object[] { null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_DefaultCouponId",
                table: "Plans",
                column: "DefaultCouponId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLedger_AccountId_CreatedAt",
                table: "AccountLedger",
                columns: new[] { "AccountId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountLedger_CompanyDealId",
                table: "AccountLedger",
                column: "CompanyDealId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLedger_CouponId",
                table: "AccountLedger",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLedger_PlanId",
                table: "AccountLedger",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_CreatedByUserId",
                table: "CompanyDeals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_Domain",
                table: "CompanyDeals",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDeals_PlanId",
                table: "CompanyDeals",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_AccountId",
                table: "CouponRedemptions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CouponRedemptions_CouponId_AccountId",
                table: "CouponRedemptions",
                columns: new[] { "CouponId", "AccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_CreatedByUserId",
                table: "Coupons",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_PlanId",
                table: "Coupons",
                column: "PlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_AgentCredentials_Agents_AgentId",
                table: "AgentCredentials",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Accounts_AccountId",
                table: "Agents",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PairingTokens_Users_UserId",
                table: "PairingTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_Coupons_DefaultCouponId",
                table: "Plans",
                column: "DefaultCouponId",
                principalTable: "Coupons",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Agents_AgentId",
                table: "Sessions",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Accounts_AccountId",
                table: "Users",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentCredentials_Agents_AgentId",
                table: "AgentCredentials");

            migrationBuilder.DropForeignKey(
                name: "FK_Agents_Accounts_AccountId",
                table: "Agents");

            migrationBuilder.DropForeignKey(
                name: "FK_PairingTokens_Users_UserId",
                table: "PairingTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Plans_Coupons_DefaultCouponId",
                table: "Plans");

            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_Agents_AgentId",
                table: "Sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Accounts_AccountId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AccountLedger");

            migrationBuilder.DropTable(
                name: "CouponRedemptions");

            migrationBuilder.DropTable(
                name: "CompanyDeals");

            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropIndex(
                name: "IX_Plans_DefaultCouponId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "IsAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultCouponId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripePriceId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripePaymentMethodFingerprint",
                table: "Accounts");

            migrationBuilder.AddColumn<int>(
                name: "TrialDays",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Accounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"),
                column: "TrialDays",
                value: 14);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "TrialDays",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"),
                column: "TrialDays",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"),
                column: "TrialDays",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"),
                column: "TrialDays",
                value: 0);

            migrationBuilder.UpdateData(
                table: "Plans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                column: "TrialDays",
                value: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentCredentials_Agents_AgentId",
                table: "AgentCredentials",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Accounts_AccountId",
                table: "Agents",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PairingTokens_Users_UserId",
                table: "PairingTokens",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_Agents_AgentId",
                table: "Sessions",
                column: "AgentId",
                principalTable: "Agents",
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
    }
}
