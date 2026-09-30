using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doorbell.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdvancedDoorbellCompositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RingButtonEntityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CameraEntityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChimeEntityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultChimeMelodyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SnapshotTimeoutSeconds = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvancedDoorbellCompositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvancedDoorbellCompositions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DoorbellRingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RingTimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SnapshotStatus = table.Column<int>(type: "int", nullable: false),
                    SnapshotStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnapshotResolution = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SnapshotCapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChimeStatus = table.Column<int>(type: "int", nullable: false),
                    ChimeMelodyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AcknowledgeStatus = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoorbellRingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoorbellRingEvents_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedDoorbellCompositions_DeviceId",
                table: "AdvancedDoorbellCompositions",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OwnerUserId",
                table: "Devices",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoorbellRingEvents_DeviceId_DeviceEventId",
                table: "DoorbellRingEvents",
                columns: new[] { "DeviceId", "DeviceEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvancedDoorbellCompositions");

            migrationBuilder.DropTable(
                name: "DoorbellRingEvents");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
