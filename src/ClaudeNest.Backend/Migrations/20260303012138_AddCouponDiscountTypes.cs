using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeNest.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponDiscountTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AmountOffCents",
                table: "Coupons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "Coupons",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "FreeMonths");

            migrationBuilder.AddColumn<int>(
                name: "DurationMonths",
                table: "Coupons",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FreeDays",
                table: "Coupons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentOff",
                table: "Coupons",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountOffCents",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "DurationMonths",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "FreeDays",
                table: "Coupons");

            migrationBuilder.DropColumn(
                name: "PercentOff",
                table: "Coupons");
        }
    }
}
