using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHotel.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCashReportBillCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bills1",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bills10",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bills100",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bills20",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bills5",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bills50",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CashTotal",
                table: "CashReports",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Coins1",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Coins10",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Coins25",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Coins5",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bills1",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Bills10",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Bills100",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Bills20",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Bills5",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Bills50",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "CashTotal",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Coins1",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Coins10",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Coins25",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Coins5",
                table: "CashReports");
        }
    }
}
