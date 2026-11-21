using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderCoveringIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt",
                table: "RESERVATION");

            migrationBuilder.DropIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt",
                table: "READER_NOTIFICATION");

            migrationBuilder.DropIndex(
                name: "IX_LOAN_ReaderId_ReturnDate_DueDate",
                table: "LOAN");

            migrationBuilder.CreateIndex(
                name: "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt",
                table: "RESERVATION",
                columns: new[] { "BookId", "Status", "ExpireAt", "CreatedAt" })
                .Annotation("SqlServer:Include", new[] { "ReaderId" });

            migrationBuilder.CreateIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt",
                table: "READER_NOTIFICATION",
                columns: new[] { "ReaderId", "IsRead", "CreatedAt" })
                .Annotation("SqlServer:Include", new[] { "NotificationId", "Title", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_ReaderId_ReturnDate_DueDate",
                table: "LOAN",
                columns: new[] { "ReaderId", "ReturnDate", "DueDate" })
                .Annotation("SqlServer:Include", new[] { "BorrowDate", "CopyId", "Status", "RenewCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt",
                table: "RESERVATION");

            migrationBuilder.DropIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt",
                table: "READER_NOTIFICATION");

            migrationBuilder.DropIndex(
                name: "IX_LOAN_ReaderId_ReturnDate_DueDate",
                table: "LOAN");

            migrationBuilder.CreateIndex(
                name: "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt",
                table: "RESERVATION",
                columns: new[] { "BookId", "Status", "ExpireAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt",
                table: "READER_NOTIFICATION",
                columns: new[] { "ReaderId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_ReaderId_ReturnDate_DueDate",
                table: "LOAN",
                columns: new[] { "ReaderId", "ReturnDate", "DueDate" });
        }
    }
}
