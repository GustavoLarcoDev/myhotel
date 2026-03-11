using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHotel.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCashReportCoinRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Rolls1",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rolls10",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rolls25",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Rolls5",
                table: "CashReports",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rolls1",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Rolls10",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Rolls25",
                table: "CashReports");

            migrationBuilder.DropColumn(
                name: "Rolls5",
                table: "CashReports");
        }
    }
}
