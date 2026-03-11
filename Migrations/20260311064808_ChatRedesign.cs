using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyHotel.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChatRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Departments_DepartmentId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_DepartmentId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Messages");

            migrationBuilder.CreateTable(
                name: "AnnouncementDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    DepartmentId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementDepartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnnouncementDepartments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementReadReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MessageId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementReadReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementReadReceipts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnnouncementReadReceipts_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementDepartments_DepartmentId",
                table: "AnnouncementDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementDepartments_MessageId",
                table: "AnnouncementDepartments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_MessageId_UserId",
                table: "AnnouncementReadReceipts",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementReadReceipts_UserId",
                table: "AnnouncementReadReceipts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementDepartments");

            migrationBuilder.DropTable(
                name: "AnnouncementReadReceipts");

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Messages",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_DepartmentId",
                table: "Messages",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Departments_DepartmentId",
                table: "Messages",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }
    }
}
