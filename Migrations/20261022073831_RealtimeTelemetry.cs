using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NhuBinh_2921.Migrations
{
    /// <inheritdoc />
    public partial class RealtimeTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastTelemetryAtUtc",
                schema: "identity",
                table: "ChildDevices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NoiseLevelDb",
                schema: "identity",
                table: "ChildDevices",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "TelemetryToken",
                schema: "identity",
                table: "ChildDevices",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastTelemetryAtUtc",
                schema: "identity",
                table: "ChildDevices");

            migrationBuilder.DropColumn(
                name: "NoiseLevelDb",
                schema: "identity",
                table: "ChildDevices");

            migrationBuilder.DropColumn(
                name: "TelemetryToken",
                schema: "identity",
                table: "ChildDevices");
        }
    }
}
