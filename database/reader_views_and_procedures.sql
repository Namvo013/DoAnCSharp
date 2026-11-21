/*
    Read-only SQL reporting objects for the reader module.
    Inventory and loan-state writes remain in LiMS.Api so the existing
    transaction, authorization and notification-outbox rules stay authoritative.
*/
SET NOCOUNT ON;
GO

CREATE OR ALTER VIEW dbo.vw_ReaderPopularBooks
AS
    SELECT b.BookId,
           b.Title,
           b.Author,
           b.Isbn,
           c.Name AS CategoryName,
           COUNT_BIG(l.LoanId) AS BorrowCount,
           COUNT_BIG(DISTINCT bc.CopyId) AS CopyCount,
           SUM(CASE WHEN bc.Status = N'Available' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END)
               AS AvailableCopyCount,
           SUM(CASE WHEN bc.Status = N'OnLoan' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END)
               AS OnLoanCopyCount
    FROM dbo.BOOK AS b
    INNER JOIN dbo.CATEGORY AS c ON c.CategoryId = b.CategoryId
    LEFT JOIN dbo.BOOK_COPY AS bc ON bc.BookId = b.BookId
    LEFT JOIN dbo.LOAN AS l ON l.CopyId = bc.CopyId
    WHERE b.IsActive = 1
    GROUP BY b.BookId, b.Title, b.Author, b.Isbn, c.Name;
GO

CREATE OR ALTER VIEW dbo.vw_ReaderLoanHistoryDetails
AS
    SELECT r.ReaderId,
           r.CardNo,
           a.AccountId,
           a.FullName AS ReaderName,
           l.LoanId,
           l.BorrowDate,
           l.DueDate,
           l.ReturnDate,
           l.Status AS LoanStatus,
           l.RenewCount,
           b.BookId,
           b.Title AS BookTitle,
           b.Author,
           b.Isbn,
           c.Name AS CategoryName,
           bc.CopyId,
           bc.Barcode,
           lib.StaffCode,
           staff.FullName AS LibrarianName,
           COALESCE(fines.TotalFine, CONVERT(decimal(10, 2), 0)) AS TotalFine,
           COALESCE(fines.UnpaidFine, CONVERT(decimal(10, 2), 0)) AS UnpaidFine
    FROM dbo.LOAN AS l
    INNER JOIN dbo.READER AS r ON r.ReaderId = l.ReaderId
    INNER JOIN dbo.ACCOUNT AS a ON a.AccountId = r.AccountId
    INNER JOIN dbo.BOOK_COPY AS bc ON bc.CopyId = l.CopyId
    INNER JOIN dbo.BOOK AS b ON b.BookId = bc.BookId
    INNER JOIN dbo.CATEGORY AS c ON c.CategoryId = b.CategoryId
    INNER JOIN dbo.LIBRARIAN AS lib ON lib.LibrarianId = l.LibrarianId
    INNER JOIN dbo.ACCOUNT AS staff ON staff.AccountId = lib.AccountId
    OUTER APPLY
    (
        SELECT SUM(f.Amount) AS TotalFine,
               SUM(CASE WHEN f.Status <> N'Paid' THEN f.Amount ELSE CONVERT(decimal(10, 2), 0) END)
                   AS UnpaidFine
        FROM dbo.FINE AS f
        WHERE f.LoanId = l.LoanId
    ) AS fines;
GO

CREATE OR ALTER VIEW dbo.vw_ReaderOverdueLoans
AS
    SELECT r.ReaderId,
           r.CardNo,
           a.AccountId,
           a.FullName AS ReaderName,
           a.Email,
           a.Phone,
           l.LoanId,
           l.BorrowDate,
           l.DueDate,
           DATEDIFF(day, l.DueDate, CONVERT(date, SYSUTCDATETIME())) AS DaysOverdue,
           b.BookId,
           b.Title AS BookTitle,
           b.Author,
           bc.CopyId,
           bc.Barcode,
           l.Status AS LoanStatus
    FROM dbo.LOAN AS l
    INNER JOIN dbo.READER AS r ON r.ReaderId = l.ReaderId
    INNER JOIN dbo.ACCOUNT AS a ON a.AccountId = r.AccountId
    INNER JOIN dbo.BOOK_COPY AS bc ON bc.CopyId = l.CopyId
    INNER JOIN dbo.BOOK AS b ON b.BookId = bc.BookId
    WHERE l.ReturnDate IS NULL
      AND l.DueDate < CONVERT(date, SYSUTCDATETIME());
GO

CREATE OR ALTER VIEW dbo.vw_ReaderReservationQueue
AS
    SELECT q.BookId,
           b.Title AS BookTitle,
           q.ReservationId,
           q.ReaderId,
           r.CardNo,
           a.FullName AS ReaderName,
           q.CreatedAt,
           q.ExpireAt,
           q.Status,
           ROW_NUMBER() OVER
           (
               PARTITION BY q.BookId
               ORDER BY CASE WHEN q.Status = N'ReadyForPickup' THEN 0 ELSE 1 END,
                        q.CreatedAt,
                        q.ReservationId
           ) AS QueuePosition
    FROM dbo.RESERVATION AS q
    INNER JOIN dbo.BOOK AS b ON b.BookId = q.BookId
    INNER JOIN dbo.READER AS r ON r.ReaderId = q.ReaderId
    INNER JOIN dbo.ACCOUNT AS a ON a.AccountId = r.AccountId
    WHERE q.Status IN (N'Pending', N'ReadyForPickup')
      AND q.ExpireAt > SYSUTCDATETIME();
GO

CREATE OR ALTER PROCEDURE dbo.usp_Reader_GetPopularBooks
    @Top int = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @Top < 1 OR @Top > 500
        THROW 51010, '@Top must be between 1 and 500.', 1;

    SELECT TOP (@Top)
           BookId, Title, Author, Isbn, CategoryName,
           BorrowCount, CopyCount, AvailableCopyCount, OnLoanCopyCount
    FROM dbo.vw_ReaderPopularBooks
    ORDER BY BorrowCount DESC, Title, BookId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Reader_GetLoanHistory
    @AccountId int,
    @PageIndex int = 1,
    @PageSize int = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @AccountId <= 0 THROW 51011, '@AccountId must be positive.', 1;
    IF @PageIndex < 1 OR @PageSize < 1 OR @PageSize > 200
        THROW 51012, 'PageIndex must be positive and PageSize must be between 1 and 200.', 1;

    SELECT ReaderId, CardNo, AccountId, ReaderName, LoanId, BorrowDate,
           DueDate, ReturnDate, LoanStatus, RenewCount, BookId, BookTitle,
           Author, Isbn, CategoryName, CopyId, Barcode, StaffCode,
           LibrarianName, TotalFine, UnpaidFine
    FROM dbo.vw_ReaderLoanHistoryDetails
    WHERE AccountId = @AccountId
    ORDER BY BorrowDate DESC, LoanId DESC
    OFFSET CONVERT(bigint, @PageIndex - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT COUNT_BIG(*) AS TotalCount
    FROM dbo.vw_ReaderLoanHistoryDetails
    WHERE AccountId = @AccountId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Reader_GetOverdueLoans
    @AsOfDate date = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @EffectiveDate date = COALESCE(@AsOfDate, CONVERT(date, SYSUTCDATETIME()));

    SELECT r.ReaderId, r.CardNo, a.AccountId, a.FullName AS ReaderName,
           a.Email, a.Phone, l.LoanId, l.BorrowDate, l.DueDate,
           DATEDIFF(day, l.DueDate, @EffectiveDate) AS DaysOverdue,
           b.BookId, b.Title AS BookTitle, b.Author, bc.CopyId,
           bc.Barcode, l.Status AS LoanStatus
    FROM dbo.LOAN AS l
    INNER JOIN dbo.READER AS r ON r.ReaderId = l.ReaderId
    INNER JOIN dbo.ACCOUNT AS a ON a.AccountId = r.AccountId
    INNER JOIN dbo.BOOK_COPY AS bc ON bc.CopyId = l.CopyId
    INNER JOIN dbo.BOOK AS b ON b.BookId = bc.BookId
    WHERE l.ReturnDate IS NULL AND l.DueDate < @EffectiveDate
    ORDER BY l.DueDate, r.ReaderId, l.LoanId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Reader_GetBookAvailability
    @BookId int
AS
BEGIN
    SET NOCOUNT ON;
    IF @BookId <= 0 THROW 51013, '@BookId must be positive.', 1;

    SELECT b.BookId,
           b.Title,
           b.IsActive,
           COUNT_BIG(bc.CopyId) AS TotalCopies,
           SUM(CASE WHEN bc.Status = N'Available' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END)
               AS AvailableCopies,
           SUM(CASE WHEN bc.Status = N'OnLoan' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END)
               AS OnLoanCopies,
           SUM(CASE WHEN bc.Status = N'Maintenance' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END)
               AS MaintenanceCopies
    FROM dbo.BOOK AS b
    LEFT JOIN dbo.BOOK_COPY AS bc ON bc.BookId = b.BookId
    WHERE b.BookId = @BookId
    GROUP BY b.BookId, b.Title, b.IsActive;
END;
GO
