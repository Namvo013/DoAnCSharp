using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiMS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialLibrarySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACCOUNT",
                columns: table => new
                {
                    AccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCOUNT", x => x.AccountId);
                    table.CheckConstraint("CK_ACCOUNT_Role", "[Role] IN ('Borrower', 'Librarian', 'Admin')");
                });

            migrationBuilder.CreateTable(
                name: "CATEGORY",
                columns: table => new
                {
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CATEGORY", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "SYSTEM_CONFIG",
                columns: table => new
                {
                    ConfigKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConfigValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYSTEM_CONFIG", x => x.ConfigKey);
                });

            migrationBuilder.CreateTable(
                name: "ACTIVITY_LOG",
                columns: table => new
                {
                    LogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetTable = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACTIVITY_LOG", x => x.LogId);
                    table.ForeignKey(
                        name: "FK_ACTIVITY_LOG_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ACCOUNT",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateTable(
                name: "LIBRARIAN",
                columns: table => new
                {
                    LibrarianId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    StaffCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LIBRARIAN", x => x.LibrarianId);
                    table.ForeignKey(
                        name: "FK_LIBRARIAN_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ACCOUNT",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateTable(
                name: "READER",
                columns: table => new
                {
                    ReaderId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    CardNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CardIssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CardExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CardStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BorrowLimit = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_READER", x => x.ReaderId);
                    table.ForeignKey(
                        name: "FK_READER_ACCOUNT_AccountId",
                        column: x => x.AccountId,
                        principalTable: "ACCOUNT",
                        principalColumn: "AccountId");
                });

            migrationBuilder.CreateTable(
                name: "BOOK",
                columns: table => new
                {
                    BookId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Isbn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PublishYear = table.Column<short>(type: "smallint", nullable: true),
                    ShelfLocation = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BOOK", x => x.BookId);
                    table.ForeignKey(
                        name: "FK_BOOK_CATEGORY_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CATEGORY",
                        principalColumn: "CategoryId");
                });

            migrationBuilder.CreateTable(
                name: "PURCHASE_PROPOSAL",
                columns: table => new
                {
                    ProposalId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<int>(type: "int", nullable: false),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PURCHASE_PROPOSAL", x => x.ProposalId);
                    table.ForeignKey(
                        name: "FK_PURCHASE_PROPOSAL_LIBRARIAN_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "LIBRARIAN",
                        principalColumn: "LibrarianId");
                    table.ForeignKey(
                        name: "FK_PURCHASE_PROPOSAL_READER_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "READER",
                        principalColumn: "ReaderId");
                });

            migrationBuilder.CreateTable(
                name: "BOOK_COPY",
                columns: table => new
                {
                    CopyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BOOK_COPY", x => x.CopyId);
                    table.ForeignKey(
                        name: "FK_BOOK_COPY_BOOK_BookId",
                        column: x => x.BookId,
                        principalTable: "BOOK",
                        principalColumn: "BookId");
                });

            migrationBuilder.CreateTable(
                name: "RESERVATION",
                columns: table => new
                {
                    ReservationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<int>(type: "int", nullable: false),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpireAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RESERVATION", x => x.ReservationId);
                    table.ForeignKey(
                        name: "FK_RESERVATION_BOOK_BookId",
                        column: x => x.BookId,
                        principalTable: "BOOK",
                        principalColumn: "BookId");
                    table.ForeignKey(
                        name: "FK_RESERVATION_READER_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "READER",
                        principalColumn: "ReaderId");
                });

            migrationBuilder.CreateTable(
                name: "LOAN",
                columns: table => new
                {
                    LoanId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<int>(type: "int", nullable: false),
                    CopyId = table.Column<int>(type: "int", nullable: false),
                    LibrarianId = table.Column<int>(type: "int", nullable: false),
                    ReservationId = table.Column<long>(type: "bigint", nullable: true),
                    BorrowDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RenewCount = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LOAN", x => x.LoanId);
                    table.ForeignKey(
                        name: "FK_LOAN_BOOK_COPY_CopyId",
                        column: x => x.CopyId,
                        principalTable: "BOOK_COPY",
                        principalColumn: "CopyId");
                    table.ForeignKey(
                        name: "FK_LOAN_LIBRARIAN_LibrarianId",
                        column: x => x.LibrarianId,
                        principalTable: "LIBRARIAN",
                        principalColumn: "LibrarianId");
                    table.ForeignKey(
                        name: "FK_LOAN_READER_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "READER",
                        principalColumn: "ReaderId");
                    table.ForeignKey(
                        name: "FK_LOAN_RESERVATION_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "RESERVATION",
                        principalColumn: "ReservationId");
                });

            migrationBuilder.CreateTable(
                name: "EMAIL_LOG",
                columns: table => new
                {
                    EmailId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<int>(type: "int", nullable: false),
                    LoanId = table.Column<long>(type: "bigint", nullable: true),
                    ReservationId = table.Column<long>(type: "bigint", nullable: true),
                    EmailType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SendStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMAIL_LOG", x => x.EmailId);
                    table.ForeignKey(
                        name: "FK_EMAIL_LOG_LOAN_LoanId",
                        column: x => x.LoanId,
                        principalTable: "LOAN",
                        principalColumn: "LoanId");
                    table.ForeignKey(
                        name: "FK_EMAIL_LOG_READER_ReaderId",
                        column: x => x.ReaderId,
                        principalTable: "READER",
                        principalColumn: "ReaderId");
                    table.ForeignKey(
                        name: "FK_EMAIL_LOG_RESERVATION_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "RESERVATION",
                        principalColumn: "ReservationId");
                });

            migrationBuilder.CreateTable(
                name: "FINE",
                columns: table => new
                {
                    FineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanId = table.Column<long>(type: "bigint", nullable: false),
                    CollectedBy = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FINE", x => x.FineId);
                    table.CheckConstraint("CK_FINE_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_FINE_LIBRARIAN_CollectedBy",
                        column: x => x.CollectedBy,
                        principalTable: "LIBRARIAN",
                        principalColumn: "LibrarianId");
                    table.ForeignKey(
                        name: "FK_FINE_LOAN_LoanId",
                        column: x => x.LoanId,
                        principalTable: "LOAN",
                        principalColumn: "LoanId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNT_Email",
                table: "ACCOUNT",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACCOUNT_Username",
                table: "ACCOUNT",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ACTIVITY_LOG_AccountId",
                table: "ACTIVITY_LOG",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ACTIVITY_LOG_CreatedAt_AccountId",
                table: "ACTIVITY_LOG",
                columns: new[] { "CreatedAt", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_BOOK_CategoryId",
                table: "BOOK",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BOOK_Isbn",
                table: "BOOK",
                column: "Isbn",
                unique: true,
                filter: "[Isbn] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BOOK_Title_Author",
                table: "BOOK",
                columns: new[] { "Title", "Author" });

            migrationBuilder.CreateIndex(
                name: "IX_BOOK_COPY_Barcode",
                table: "BOOK_COPY",
                column: "Barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BOOK_COPY_BookId_Status",
                table: "BOOK_COPY",
                columns: new[] { "BookId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CATEGORY_Name",
                table: "CATEGORY",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EMAIL_LOG_LoanId",
                table: "EMAIL_LOG",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_EMAIL_LOG_ReaderId_SentAt",
                table: "EMAIL_LOG",
                columns: new[] { "ReaderId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EMAIL_LOG_ReservationId",
                table: "EMAIL_LOG",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_FINE_CollectedBy",
                table: "FINE",
                column: "CollectedBy");

            migrationBuilder.CreateIndex(
                name: "IX_FINE_LoanId",
                table: "FINE",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_LIBRARIAN_AccountId",
                table: "LIBRARIAN",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LIBRARIAN_StaffCode",
                table: "LIBRARIAN",
                column: "StaffCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_CopyId",
                table: "LOAN",
                column: "CopyId",
                unique: true,
                filter: "[ReturnDate] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_LibrarianId",
                table: "LOAN",
                column: "LibrarianId");

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_ReaderId_BorrowDate",
                table: "LOAN",
                columns: new[] { "ReaderId", "BorrowDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LOAN_ReservationId",
                table: "LOAN",
                column: "ReservationId",
                unique: true,
                filter: "[ReservationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PURCHASE_PROPOSAL_ReaderId",
                table: "PURCHASE_PROPOSAL",
                column: "ReaderId");

            migrationBuilder.CreateIndex(
                name: "IX_PURCHASE_PROPOSAL_ReviewedBy",
                table: "PURCHASE_PROPOSAL",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_READER_AccountId",
                table: "READER",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_READER_CardNo",
                table: "READER",
                column: "CardNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RESERVATION_BookId",
                table: "RESERVATION",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_RESERVATION_ReaderId_BookId_Status",
                table: "RESERVATION",
                columns: new[] { "ReaderId", "BookId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACTIVITY_LOG");

            migrationBuilder.DropTable(
                name: "EMAIL_LOG");

            migrationBuilder.DropTable(
                name: "FINE");

            migrationBuilder.DropTable(
                name: "PURCHASE_PROPOSAL");

            migrationBuilder.DropTable(
                name: "SYSTEM_CONFIG");

            migrationBuilder.DropTable(
                name: "LOAN");

            migrationBuilder.DropTable(
                name: "BOOK_COPY");

            migrationBuilder.DropTable(
                name: "LIBRARIAN");

            migrationBuilder.DropTable(
                name: "RESERVATION");

            migrationBuilder.DropTable(
                name: "BOOK");

            migrationBuilder.DropTable(
                name: "READER");

            migrationBuilder.DropTable(
                name: "CATEGORY");

            migrationBuilder.DropTable(
                name: "ACCOUNT");
        }
    }
}
