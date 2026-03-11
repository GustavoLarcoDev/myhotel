using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHotel.Web.Migrations
{
    /// <inheritdoc />
    public partial class MakeHotelIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserHotelRoles_Hotels_HotelId",
                table: "UserHotelRoles");

            migrationBuilder.AlterColumn<int>(
                name: "HotelId",
                table: "UserHotelRoles",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_UserHotelRoles_Hotels_HotelId",
                table: "UserHotelRoles",
                column: "HotelId",
                principalTable: "Hotels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserHotelRoles_Hotels_HotelId",
                table: "UserHotelRoles");

            migrationBuilder.AlterColumn<int>(
                name: "HotelId",
                table: "UserHotelRoles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserHotelRoles_Hotels_HotelId",
                table: "UserHotelRoles",
                column: "HotelId",
                principalTable: "Hotels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
