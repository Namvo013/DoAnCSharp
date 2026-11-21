using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NOTIFICATION_OUTBOX",
                columns: table => new
                {
                    OutboxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReservationId = table.Column<long>(type: "bigint", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTIFICATION_OUTBOX", x => x.OutboxId);
                    table.ForeignKey(
                        name: "FK_NOTIFICATION_OUTBOX_READER_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "READER",
                        principalColumn: "ReaderId");
                });

            migrationBuilder.CreateTable(
                name: "READER_NOTIFICATION",
                columns: table => new
                {
                    NotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RelatedEntityId = table.Column<long>(type: "bigint", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_READER_NOTIFICATION", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_READER_NOTIFICATION_READER_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "READER",
                        principalColumn: "ReaderId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_OUTBOX_DeduplicationKey",
                table: "NOTIFICATION_OUTBOX",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_OUTBOX_ProcessedAt_CreatedAt",
                table: "NOTIFICATION_OUTBOX",
                columns: new[] { "ProcessedAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_OUTBOX_ReaderId",
                table: "NOTIFICATION_OUTBOX",
                column: "ReaderId");

            migrationBuilder.CreateIndex(
                name: "IX_READER_NOTIFICATION_DeduplicationKey",
                table: "READER_NOTIFICATION",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_CreatedAt",
                table: "READER_NOTIFICATION",
                columns: new[] { "ReaderId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_IsRead",
                table: "READER_NOTIFICATION",
                columns: new[] { "ReaderId", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NOTIFICATION_OUTBOX");

            migrationBuilder.DropTable(
                name: "READER_NOTIFICATION");
        }
    }
}
