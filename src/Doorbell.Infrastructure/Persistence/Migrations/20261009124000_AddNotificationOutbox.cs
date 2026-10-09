using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Doorbell.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationOutboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RingEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
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
                name: "IX_NotificationAttempts_OutboxId_AttemptNumber",
                table: "NotificationAttempts",
                columns: new[] { "OutboxId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_RecipientUserId",
                table: "NotificationOutboxes",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationOutboxes_RingEventId",
                table: "NotificationOutboxes",
                column: "RingEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationAttempts");

            migrationBuilder.DropTable(
                name: "NotificationOutboxes");
        }
    }
}
