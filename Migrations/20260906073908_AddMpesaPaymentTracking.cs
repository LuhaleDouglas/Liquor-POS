using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiquorPOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMpesaPaymentTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MerchantRequestId",
                table: "SalePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutRequestId",
                table: "SalePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MpesaReceiptNumber",
                table: "SalePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MpesaTransactionDate",
                table: "SalePayments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MpesaPhoneNumber",
                table: "SalePayments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MpesaResultCode",
                table: "SalePayments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MpesaResultDescription",
                table: "SalePayments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MerchantRequestId",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "CheckoutRequestId",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "MpesaReceiptNumber",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "MpesaTransactionDate",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "MpesaPhoneNumber",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "MpesaResultCode",
                table: "SalePayments");

            migrationBuilder.DropColumn(
                name: "MpesaResultDescription",
                table: "SalePayments");
        }
    }
}