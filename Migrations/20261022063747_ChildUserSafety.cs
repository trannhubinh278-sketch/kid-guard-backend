using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhuBinh_2921.Migrations
{
    /// <inheritdoc />
    public partial class ChildUserSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChildUserId",
                schema: "identity",
                table: "ChildDevices",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PairedAtUtc",
                schema: "identity",
                table: "ChildDevices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SafetyAlerts",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildDeviceId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyAlerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildDevices_ChildUserId",
                schema: "identity",
                table: "ChildDevices",
                column: "ChildUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyAlerts_ChildDeviceId",
                schema: "identity",
                table: "SafetyAlerts",
                column: "ChildDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SafetyAlerts",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_ChildDevices_ChildUserId",
                schema: "identity",
                table: "ChildDevices");

            migrationBuilder.DropColumn(
                name: "ChildUserId",
                schema: "identity",
                table: "ChildDevices");

            migrationBuilder.DropColumn(
                name: "PairedAtUtc",
                schema: "identity",
                table: "ChildDevices");
        }
    }
}
