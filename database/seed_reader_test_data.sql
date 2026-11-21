/* Fixture for an isolated LiMS test/dev database. It is safe to rerun.
   Accounts are disabled and use an invalid hash; no test login is created. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF DB_NAME() NOT LIKE N'%test%' AND DB_NAME() NOT LIKE N'%dev%'
    THROW 51020, 'Seed aborted: database name must contain test or dev.', 1;
GO
BEGIN TRANSACTION;
DECLARE @Today date = CONVERT(date, SYSUTCDATETIME());
DECLARE @CategoryId int, @ReaderAccountId int, @OtherAccountId int;
DECLARE @StaffAccountId int, @ReaderId int, @OtherReaderId int, @StaffId int;
DECLARE @BookId int, @OtherBookId int, @CopyId int, @ReturnedCopyId int;

IF NOT EXISTS (SELECT 1 FROM dbo.CATEGORY WHERE Name = N'LiMS Fixture - Reader')
    INSERT dbo.CATEGORY (Name) VALUES (N'LiMS Fixture - Reader');
SELECT @CategoryId = CategoryId FROM dbo.CATEGORY WHERE Name = N'LiMS Fixture - Reader';

IF NOT EXISTS (SELECT 1 FROM dbo.ACCOUNT WHERE Username = N'lims-fixture-reader')
    INSERT dbo.ACCOUNT (Username, PasswordHash, Email, FullName, Phone, Role, IsActive, CreatedAt)
    VALUES (N'lims-fixture-reader', N'INVALID_FIXTURE_HASH_DISABLED', N'reader@fixture.invalid',
        N'Độc giả kiểm thử', N'0000000001', N'Borrower', 0, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.ACCOUNT WHERE Username = N'lims-fixture-reader-2')
    INSERT dbo.ACCOUNT (Username, PasswordHash, Email, FullName, Phone, Role, IsActive, CreatedAt)
    VALUES (N'lims-fixture-reader-2', N'INVALID_FIXTURE_HASH_DISABLED', N'reader2@fixture.invalid',
        N'Độc giả kiểm thử 2', N'0000000002', N'Borrower', 0, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.ACCOUNT WHERE Username = N'lims-fixture-librarian')
    INSERT dbo.ACCOUNT (Username, PasswordHash, Email, FullName, Phone, Role, IsActive, CreatedAt)
    VALUES (N'lims-fixture-librarian', N'INVALID_FIXTURE_HASH_DISABLED', N'staff@fixture.invalid',
        N'Thủ thư kiểm thử', N'0000000003', N'Librarian', 0, SYSUTCDATETIME());

SELECT @ReaderAccountId = AccountId FROM dbo.ACCOUNT WHERE Username = N'lims-fixture-reader';
SELECT @OtherAccountId = AccountId FROM dbo.ACCOUNT WHERE Username = N'lims-fixture-reader-2';
SELECT @StaffAccountId = AccountId FROM dbo.ACCOUNT WHERE Username = N'lims-fixture-librarian';
UPDATE dbo.ACCOUNT
SET IsActive = 0, PasswordHash = N'INVALID_FIXTURE_HASH_DISABLED'
WHERE AccountId IN (@ReaderAccountId, @OtherAccountId, @StaffAccountId);
IF NOT EXISTS (SELECT 1 FROM dbo.READER WHERE AccountId = @ReaderAccountId)
    INSERT dbo.READER (AccountId, CardNo, CardIssueDate, CardExpiryDate, CardStatus, BorrowLimit)
    VALUES (@ReaderAccountId, N'FIXTURE-R-001', DATEADD(day, -30, @Today), DATEADD(year, 1, @Today), N'Active', 5);
IF NOT EXISTS (SELECT 1 FROM dbo.READER WHERE AccountId = @OtherAccountId)
    INSERT dbo.READER (AccountId, CardNo, CardIssueDate, CardExpiryDate, CardStatus, BorrowLimit)
    VALUES (@OtherAccountId, N'FIXTURE-R-002', DATEADD(day, -30, @Today), DATEADD(year, 1, @Today), N'Active', 3);
IF NOT EXISTS (SELECT 1 FROM dbo.LIBRARIAN WHERE AccountId = @StaffAccountId)
    INSERT dbo.LIBRARIAN (AccountId, StaffCode) VALUES (@StaffAccountId, N'FIXTURE-ST-01');
SELECT @ReaderId = ReaderId FROM dbo.READER WHERE AccountId = @ReaderAccountId;
SELECT @OtherReaderId = ReaderId FROM dbo.READER WHERE AccountId = @OtherAccountId;
SELECT @StaffId = LibrarianId FROM dbo.LIBRARIAN WHERE AccountId = @StaffAccountId;

IF NOT EXISTS (SELECT 1 FROM dbo.BOOK WHERE Isbn = N'9780000000001')
    INSERT dbo.BOOK (CategoryId, Title, Author, Isbn, Publisher, PublishYear,
                     ShelfLocation, Description, IsActive)
    VALUES (@CategoryId, N'Fixture - Distributed Systems', N'Fixture Author A', N'9780000000001',
        N'Fixture Press', 2024, N'TEST-A-01', N'Dữ liệu kiểm thử tìm kiếm và đặt giữ.', 1);
IF NOT EXISTS (SELECT 1 FROM dbo.BOOK WHERE Isbn = N'9780000000002')
    INSERT dbo.BOOK (CategoryId, Title, Author, Isbn, Publisher, PublishYear,
                     ShelfLocation, Description, IsActive)
    VALUES (@CategoryId, N'Fixture - Database Design', N'Fixture Author B', N'9780000000002',
        N'Fixture Press', 2023, N'TEST-A-02', N'Dữ liệu kiểm thử lịch sử mượn trả.', 1);
SELECT @BookId = BookId FROM dbo.BOOK WHERE Isbn = N'9780000000001';
SELECT @OtherBookId = BookId FROM dbo.BOOK WHERE Isbn = N'9780000000002';

IF NOT EXISTS (SELECT 1 FROM dbo.BOOK_COPY WHERE Barcode = N'FIXTURE-COPY-001')
    INSERT dbo.BOOK_COPY (BookId, Barcode, Status) VALUES (@BookId, N'FIXTURE-COPY-001', N'OnLoan');
IF NOT EXISTS (SELECT 1 FROM dbo.BOOK_COPY WHERE Barcode = N'FIXTURE-COPY-002')
    INSERT dbo.BOOK_COPY (BookId, Barcode, Status) VALUES (@BookId, N'FIXTURE-COPY-002', N'Maintenance');
IF NOT EXISTS (SELECT 1 FROM dbo.BOOK_COPY WHERE Barcode = N'FIXTURE-COPY-003')
    INSERT dbo.BOOK_COPY (BookId, Barcode, Status) VALUES (@OtherBookId, N'FIXTURE-COPY-003', N'Available');
SELECT @CopyId = CopyId FROM dbo.BOOK_COPY WHERE Barcode = N'FIXTURE-COPY-001';
SELECT @ReturnedCopyId = CopyId FROM dbo.BOOK_COPY WHERE Barcode = N'FIXTURE-COPY-003';

IF NOT EXISTS (SELECT 1 FROM dbo.LOAN WHERE CopyId = @CopyId AND ReturnDate IS NULL)
    INSERT dbo.LOAN (ReaderId, CopyId, LibrarianId, ReservationId, BorrowDate,
                     DueDate, ReturnDate, RenewCount, Status)
    VALUES (@ReaderId, @CopyId, @StaffId, NULL, DATEADD(day, -5, SYSUTCDATETIME()),
        DATEADD(day, 9, @Today), NULL, 0, N'Borrowed');
IF NOT EXISTS (SELECT 1 FROM dbo.LOAN WHERE CopyId = @ReturnedCopyId)
    INSERT dbo.LOAN (ReaderId, CopyId, LibrarianId, ReservationId, BorrowDate,
                     DueDate, ReturnDate, RenewCount, Status)
    VALUES (@OtherReaderId, @ReturnedCopyId, @StaffId, NULL, DATEADD(day, -40, SYSUTCDATETIME()),
        DATEADD(day, -26, @Today), DATEADD(day, -25, SYSUTCDATETIME()), 1, N'Returned');
DECLARE @ReturnedLoanId bigint =
    (SELECT TOP (1) LoanId FROM dbo.LOAN WHERE CopyId = @ReturnedCopyId ORDER BY LoanId);
IF NOT EXISTS (SELECT 1 FROM dbo.FINE WHERE LoanId = @ReturnedLoanId AND Reason = N'LiMS fixture fee')
    INSERT dbo.FINE (LoanId, CollectedBy, Amount, Reason, Status, PaidAt)
    VALUES (@ReturnedLoanId, NULL, 25000, N'LiMS fixture fee', N'Paid', DATEADD(day, -24, SYSUTCDATETIME()));
IF NOT EXISTS (SELECT 1 FROM dbo.RESERVATION WHERE ReaderId = @OtherReaderId
               AND BookId = @BookId AND Status = N'Pending')
    INSERT dbo.RESERVATION (ReaderId, BookId, CreatedAt, ExpireAt, Status)
    VALUES (@OtherReaderId, @BookId, SYSUTCDATETIME(), DATEADD(day, 7, SYSUTCDATETIME()), N'Pending');

COMMIT TRANSACTION;
SELECT N'Fixture is ready; both accounts are disabled.' AS Result,
       DB_NAME() AS DatabaseName, @ReaderId AS ReaderId,
       @OtherReaderId AS OtherReaderId, @BookId AS BookId, @OtherBookId AS OtherBookId;
GO
