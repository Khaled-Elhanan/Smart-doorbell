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
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubjectId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
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
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
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

            migrationBuilder.CreateTable(
                name: "NotificationOutboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationOutboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationOutboxes_DoorbellRingEvents_RingEventId",
                        column: x => x.RingEventId,
                        principalTable: "DoorbellRingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationOutboxes_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    AttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Channel = table.Column<int>(type: "int", nullable: true),
                    UsedFallback = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationAttempts_NotificationOutboxes_OutboxId",
                        column: x => x.OutboxId,
                        principalTable: "NotificationOutboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvancedDoorbellCompositions_DeviceId",
                table: "AdvancedDoorbellCompositions",
                column: "DeviceId",
                unique: true);

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
                name: "IX_DeviceCommands_RingEventId_CommandType",
                table: "DeviceCommands",
                columns: new[] { "RingEventId", "CommandType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_StorageKey",
                table: "DeviceCommands",
                column: "StorageKey");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OwnerUserId",
                table: "Devices",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoorbellRingEvents_DeviceId_DeviceEventId",
                table: "DoorbellRingEvents",
                columns: new[] { "DeviceId", "DeviceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAttempts_OutboxId_AttemptNumber",
                table: "NotificationAttempts",
                columns: new[] { "OutboxId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_RecipientUserId",
                table: "NotificationOutboxes",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_RingEventId_Kind",
                table: "NotificationOutboxes",
                columns: new[] { "RingEventId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_SubjectId",
                table: "Users",
                column: "SubjectId",
                unique: true,
                filter: "[SubjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvancedDoorbellCompositions");

            migrationBuilder.DropTable(
                name: "DeviceCommands");

            migrationBuilder.DropTable(
                name: "NotificationAttempts");

            migrationBuilder.DropTable(
                name: "NotificationOutboxes");

            migrationBuilder.DropTable(
                name: "DoorbellRingEvents");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
