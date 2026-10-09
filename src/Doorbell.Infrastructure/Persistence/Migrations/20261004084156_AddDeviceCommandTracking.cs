using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doorbell.Infrastructure.Persistence.Migrations
{
    public partial class AddDeviceCommandTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeviceCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CommandType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCommands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceCommands_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeviceCommands_DoorbellRingEvents_RingEventId",
                        column: x => x.RingEventId,
                        principalTable: "DoorbellRingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_DeviceId",
                table: "DeviceCommands",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_RequestId",
                table: "DeviceCommands",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_RingEventId",
                table: "DeviceCommands",
                column: "RingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_StorageKey",
                table: "DeviceCommands",
                column: "StorageKey");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCommands");
        }
    }
}
