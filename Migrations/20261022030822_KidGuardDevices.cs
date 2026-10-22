using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhuBinh_2921.Migrations
{
    /// <inheritdoc />
    public partial class KidGuardDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChildDevices",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PairingCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ParentUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    LastLocationAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BatteryPercent = table.Column<int>(type: "int", nullable: false),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    IsInsideSafeZone = table.Column<bool>(type: "bit", nullable: false),
                    MotionLevel = table.Column<double>(type: "float", nullable: false),
                    SafeZoneName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SafeZoneLatitude = table.Column<double>(type: "float", nullable: false),
                    SafeZoneLongitude = table.Column<double>(type: "float", nullable: false),
                    SafeZoneRadiusMeters = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildDevices_PairingCode",
                schema: "identity",
                table: "ChildDevices",
                column: "PairingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildDevices_ParentUserId",
                schema: "identity",
                table: "ChildDevices",
                column: "ParentUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildDevices",
                schema: "identity");
        }
    }
}
