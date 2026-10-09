using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doorbell.Infrastructure.Persistence.Migrations
{
    public partial class AddConcurrencyAndCommandIdempotency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceCommands_RingEventId",
                table: "DeviceCommands");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DoorbellRingEvents",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "DeviceCommands",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_RingEventId_CommandType",
                table: "DeviceCommands",
                columns: new[] { "RingEventId", "CommandType" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeviceCommands_RingEventId_CommandType",
                table: "DeviceCommands");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DoorbellRingEvents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "DeviceCommands");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_RingEventId",
                table: "DeviceCommands",
                column: "RingEventId");
        }
    }
}
