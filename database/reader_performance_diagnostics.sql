/*
   Read-only measurements for the reader query workload.
   Run against a restored test/staging database after migrations and indexes.
   In SSMS, enable Include Actual Execution Plan (Ctrl+M) before executing.
   No schema or application data is changed by this script.
*/
SET NOCOUNT ON;
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

IF OBJECT_ID(N'dbo.READER', N'U') IS NULL
    THROW 51030, 'LiMS schema is not installed in the current database.', 1;
GO

DECLARE @ReaderAccountId int = NULL;
DECLARE @ReaderId int = NULL;
DECLARE @BookId int = NULL;
DECLARE @ReaderCardNo nvarchar(20) = N'FIXTURE-R-001';
DECLARE @BookTitle nvarchar(255) = N'Fixture';

SELECT TOP (1) @ReaderId = ReaderId, @ReaderAccountId = AccountId
FROM dbo.READER
WHERE CardNo = @ReaderCardNo;
SELECT TOP (1) @BookId = BookId
FROM dbo.BOOK
WHERE Title LIKE N'%' + @BookTitle + N'%'
ORDER BY Title, BookId;

IF @ReaderId IS NULL
    PRINT 'Reader fixture not found: reader-only statements will return empty results.';
IF @BookId IS NULL
    PRINT 'Book fixture not found: book-specific statements will return empty results.';
GO

/* A. Catalog list projection: title ordering and per-book inventory counts. */
DECLARE @CatalogSearch nvarchar(255) = N'Fixture';
SELECT b.BookId, b.Title, b.Author, b.Isbn, c.Name AS CategoryName,
       COUNT_BIG(bc.CopyId) AS CopyCount,
       SUM(CASE WHEN bc.Status = N'Available' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS AvailableCopies,
       SUM(CASE WHEN bc.Status = N'OnLoan' THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS OnLoanCopies
FROM dbo.BOOK AS b
INNER JOIN dbo.CATEGORY AS c ON c.CategoryId = b.CategoryId
LEFT JOIN dbo.BOOK_COPY AS bc ON bc.BookId = b.BookId
WHERE b.IsActive = 1
  AND (@CatalogSearch IS NULL OR b.Title LIKE N'%' + @CatalogSearch + N'%'
       OR b.Author LIKE N'%' + @CatalogSearch + N'%'
       OR b.Isbn LIKE N'%' + @CatalogSearch + N'%'
       OR c.Name LIKE N'%' + @CatalogSearch + N'%')
GROUP BY b.BookId, b.Title, b.Author, b.Isbn, c.Name
ORDER BY b.Title, b.BookId;
GO

/* B. Reader loan history: compare the actual plan with IX_LOAN_ReaderId_BorrowDate. */
DECLARE @HistoryReaderId int =
    (SELECT TOP (1) ReaderId FROM dbo.READER WHERE CardNo = N'FIXTURE-R-001');
SELECT TOP (100) l.LoanId, l.BorrowDate, l.DueDate, l.ReturnDate, l.Status,
       l.RenewCount, b.BookId, b.Title, b.Author, bc.Barcode, c.Name AS CategoryName
FROM dbo.LOAN AS l
INNER JOIN dbo.BOOK_COPY AS bc ON bc.CopyId = l.CopyId
INNER JOIN dbo.BOOK AS b ON b.BookId = bc.BookId
INNER JOIN dbo.CATEGORY AS c ON c.CategoryId = b.CategoryId
WHERE l.ReaderId = @HistoryReaderId
ORDER BY l.BorrowDate DESC, l.LoanId DESC;
GO

/* C. Open/overdue reader loans: compare with the reader/date composite index. */
DECLARE @OverdueReaderId int =
    (SELECT TOP (1) ReaderId FROM dbo.READER WHERE CardNo = N'FIXTURE-R-001');
SELECT l.LoanId, l.ReaderId, l.DueDate, l.BorrowDate, bc.Barcode, b.Title
FROM dbo.LOAN AS l
INNER JOIN dbo.BOOK_COPY AS bc ON bc.CopyId = l.CopyId
INNER JOIN dbo.BOOK AS b ON b.BookId = bc.BookId
WHERE l.ReaderId = @OverdueReaderId
  AND l.ReturnDate IS NULL
  AND l.DueDate < CONVERT(date, SYSUTCDATETIME())
ORDER BY l.DueDate, l.LoanId;
GO

/* D. Reservation FIFO selection and expired-reservation sweep predicates. */
DECLARE @QueueBookId int =
    (SELECT TOP (1) BookId FROM dbo.BOOK WHERE Isbn = N'9780000000001');
SELECT TOP (1) q.ReservationId, q.ReaderId, q.CreatedAt, q.ExpireAt, q.Status
FROM dbo.RESERVATION AS q
WHERE q.BookId = @QueueBookId AND q.Status = N'Pending'
  AND q.ExpireAt > SYSUTCDATETIME()
ORDER BY q.CreatedAt, q.ReservationId;
SELECT q.ReservationId, q.ReaderId, q.Status, q.ExpireAt
FROM dbo.RESERVATION AS q
WHERE q.BookId = @QueueBookId
  AND q.Status IN (N'Pending', N'ReadyForPickup')
  AND q.ExpireAt <= SYSUTCDATETIME();
GO

/* E. Reader inbox and unread count. */
DECLARE @InboxReaderId int =
    (SELECT TOP (1) ReaderId FROM dbo.READER WHERE CardNo = N'FIXTURE-R-001');
SELECT TOP (50) NotificationId, Title, Type, CreatedAt, IsRead
FROM dbo.READER_NOTIFICATION
WHERE ReaderId = @InboxReaderId
ORDER BY CreatedAt DESC, NotificationId DESC;
SELECT COUNT_BIG(*) AS UnreadCount
FROM dbo.READER_NOTIFICATION
WHERE ReaderId = @InboxReaderId AND IsRead = 0;
GO

/* F. Outbox batch scan and copy availability aggregation. */
SELECT TOP (100) OutboxId, ReaderId, ReservationId, EventType, CreatedAt, Attempts
FROM dbo.NOTIFICATION_OUTBOX
WHERE ProcessedAt IS NULL
ORDER BY CreatedAt, OutboxId;

DECLARE @AvailabilityBookId int =
    (SELECT TOP (1) BookId FROM dbo.BOOK WHERE Isbn = N'9780000000001');
SELECT BookId, Status, COUNT_BIG(*) AS CopyCount
FROM dbo.BOOK_COPY
WHERE BookId = @AvailabilityBookId
GROUP BY BookId, Status;
GO

/* Index inventory and usage since the last SQL Server restart or index reset. */
SELECT OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
       OBJECT_NAME(i.object_id) AS TableName,
       i.name AS IndexName,
       COALESCE(u.user_seeks, 0) AS UserSeeks,
       COALESCE(u.user_scans, 0) AS UserScans,
       COALESCE(u.user_lookups, 0) AS UserLookups,
       COALESCE(u.user_updates, 0) AS UserUpdates,
       i.filter_definition AS FilterDefinition
FROM sys.indexes AS i
LEFT JOIN sys.dm_db_index_usage_stats AS u
  ON u.database_id = DB_ID() AND u.object_id = i.object_id AND u.index_id = i.index_id
WHERE i.object_id IN (OBJECT_ID(N'dbo.BOOK'), OBJECT_ID(N'dbo.BOOK_COPY'),
                      OBJECT_ID(N'dbo.LOAN'), OBJECT_ID(N'dbo.RESERVATION'),
                      OBJECT_ID(N'dbo.READER_NOTIFICATION'), OBJECT_ID(N'dbo.NOTIFICATION_OUTBOX'))
  AND i.index_id > 0
ORDER BY TableName, IndexName;
GO

/* Row counts and statistics freshness for the reader workload tables. */
WITH TableRows AS
(
    SELECT object_id, SUM(rows) AS ApproximateRows
    FROM sys.partitions
    WHERE index_id IN (0, 1)
    GROUP BY object_id
),
LatestStats AS
(
    SELECT object_id, MAX(STATS_DATE(object_id, stats_id)) AS LatestStatisticsUpdate
    FROM sys.stats
    GROUP BY object_id
)
SELECT OBJECT_SCHEMA_NAME(t.object_id) AS SchemaName,
       OBJECT_NAME(t.object_id) AS TableName,
       t.ApproximateRows,
       s.LatestStatisticsUpdate
FROM TableRows AS t
LEFT JOIN LatestStats AS s ON s.object_id = t.object_id
WHERE t.object_id IN (OBJECT_ID(N'dbo.BOOK'), OBJECT_ID(N'dbo.BOOK_COPY'),
                      OBJECT_ID(N'dbo.LOAN'), OBJECT_ID(N'dbo.RESERVATION'),
                      OBJECT_ID(N'dbo.READER_NOTIFICATION'), OBJECT_ID(N'dbo.NOTIFICATION_OUTBOX'))
ORDER BY TableName;
GO

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO
