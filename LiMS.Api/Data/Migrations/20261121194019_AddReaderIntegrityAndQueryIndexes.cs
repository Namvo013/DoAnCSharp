using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderIntegrityAndQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RESERVATION_BookId",
                table: "RESERVATION");

            migrationBuilder.CreateIndex(
                name: "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt",
                table: "RESERVATION",
                columns: new[] { "BookId", "Status", "ExpireAt", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RESERVATION_ExpiryAfterCreated",
                table: "RESERVATION",
                sql: "[ExpireAt] >= [CreatedAt]");

            migrationBuilder.CreateIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt",
                table: "READER_NOTIFICATION",
                columns: new[] { "ReaderId", "IsRead", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_READER_BorrowLimit",
                table: "READER",
                sql: "[BorrowLimit] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_READER_CardDateRange",
                table: "READER",
                sql: "[CardExpiryDate] >= [CardIssueDate]");

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_ReaderId_ReturnDate_DueDate",
                table: "LOAN",
                columns: new[] { "ReaderId", "ReturnDate", "DueDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LOAN_DueDateAfterBorrowDate",
                table: "LOAN",
                sql: "[DueDate] >= CONVERT(date, [BorrowDate])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LOAN_RenewCount",
                table: "LOAN",
                sql: "[RenewCount] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt",
                table: "RESERVATION");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RESERVATION_ExpiryAfterCreated",
                table: "RESERVATION");

            migrationBuilder.DropIndex(
                name: "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt",
                table: "READER_NOTIFICATION");

            migrationBuilder.DropCheckConstraint(
                name: "CK_READER_BorrowLimit",
                table: "READER");

            migrationBuilder.DropCheckConstraint(
                name: "CK_READER_CardDateRange",
                table: "READER");

            migrationBuilder.DropIndex(
                name: "IX_LOAN_ReaderId_ReturnDate_DueDate",
                table: "LOAN");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LOAN_DueDateAfterBorrowDate",
                table: "LOAN");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LOAN_RenewCount",
                table: "LOAN");

            migrationBuilder.CreateIndex(
                name: "IX_RESERVATION_BookId",
                table: "RESERVATION",
                column: "BookId");
        }
    }
}
