/*
    LiMS SQL Server index and integrity deployment script.
    Safe to run repeatedly. Schema names match LiMSDbContext migrations.
    Run during a maintenance window on large databases; index builds can lock writes.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'dbo.READER', N'U') IS NULL
    THROW 51004, 'Run the LiMS database migrations before applying this script.', 1;
IF OBJECT_ID(N'dbo.RESERVATION', N'U') IS NULL
    THROW 51004, 'Run the LiMS database migrations before applying this script.', 1;
IF OBJECT_ID(N'dbo.LOAN', N'U') IS NULL
    THROW 51004, 'Run the LiMS database migrations before applying this script.', 1;

/*
    Preflight: report legacy rows that would violate the new EF migration checks.
    The script deliberately stops instead of silently enabling an untrusted check.
*/
IF EXISTS (SELECT 1 FROM dbo.READER WHERE CardExpiryDate < CardIssueDate OR BorrowLimit < 0)
    THROW 51001, 'READER contains invalid card dates or a negative borrow limit.', 1;
IF EXISTS (SELECT 1 FROM dbo.RESERVATION WHERE ExpireAt < CreatedAt)
    THROW 51002, 'RESERVATION contains an expiry earlier than its creation date.', 1;
IF EXISTS (SELECT 1 FROM dbo.LOAN WHERE DueDate < CONVERT(date, BorrowDate) OR RenewCount < 0)
    THROW 51003, 'LOAN contains an invalid due date or renewal count.', 1;
GO

/* Reader loan-history lookup and active-loan filtering. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LOAN')
    AND name = N'IX_LOAN_ReaderId_ReturnDate_DueDate')
    CREATE INDEX IX_LOAN_ReaderId_ReturnDate_DueDate
        ON dbo.LOAN (ReaderId, ReturnDate, DueDate)
        INCLUDE (BorrowDate, CopyId, Status, RenewCount);
GO

/* Reservation queue selection by book, state, expiry, then FIFO creation time. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RESERVATION')
    AND name = N'IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt')
    CREATE INDEX IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt
        ON dbo.RESERVATION (BookId, Status, ExpireAt, CreatedAt)
        INCLUDE (ReaderId);
GO

/* Notification inbox list and unread-count operations for one reader. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.READER_NOTIFICATION')
    AND name = N'IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt')
    CREATE INDEX IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt
        ON dbo.READER_NOTIFICATION (ReaderId, IsRead, CreatedAt DESC)
        INCLUDE (NotificationId, Title, Type);
GO

/* Outbox workers scan unprocessed events by creation time. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.NOTIFICATION_OUTBOX')
    AND name = N'IX_NOTIFICATION_OUTBOX_Pending_CreatedAt')
    CREATE INDEX IX_NOTIFICATION_OUTBOX_Pending_CreatedAt
        ON dbo.NOTIFICATION_OUTBOX (CreatedAt, OutboxId)
        INCLUDE (ReaderId, ReservationId, EventType, Attempts)
        WHERE ProcessedAt IS NULL;
GO

/* A copy can have at most one active loan; the existing filtered unique index
   owns concurrency. This additional index accelerates title availability counts. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.BOOK_COPY')
    AND name = N'IX_BOOK_COPY_BookId_Status_CopyId')
    CREATE INDEX IX_BOOK_COPY_BookId_Status_CopyId
        ON dbo.BOOK_COPY (BookId, Status, CopyId);
GO

/* Keep checks aligned with the EF migration. Existing constraints are not replaced. */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_READER_CardDateRange')
    ALTER TABLE dbo.READER WITH CHECK ADD CONSTRAINT CK_READER_CardDateRange
        CHECK (CardExpiryDate >= CardIssueDate);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_READER_BorrowLimit')
    ALTER TABLE dbo.READER WITH CHECK ADD CONSTRAINT CK_READER_BorrowLimit
        CHECK (BorrowLimit >= 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_RESERVATION_ExpiryAfterCreated')
    ALTER TABLE dbo.RESERVATION WITH CHECK ADD CONSTRAINT CK_RESERVATION_ExpiryAfterCreated
        CHECK (ExpireAt >= CreatedAt);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_LOAN_DueDateAfterBorrowDate')
    ALTER TABLE dbo.LOAN WITH CHECK ADD CONSTRAINT CK_LOAN_DueDateAfterBorrowDate
        CHECK (DueDate >= CONVERT(date, BorrowDate));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_LOAN_RenewCount')
    ALTER TABLE dbo.LOAN WITH CHECK ADD CONSTRAINT CK_LOAN_RenewCount
        CHECK (RenewCount >= 0);
GO

/* Review actual plans and usage before removing any older index. */
SELECT OBJECT_SCHEMA_NAME(i.object_id) AS SchemaName,
       OBJECT_NAME(i.object_id) AS TableName,
       i.name AS IndexName,
       i.type_desc AS IndexType,
       i.is_unique AS IsUnique,
       i.has_filter AS HasFilter,
       i.filter_definition AS FilterDefinition
FROM sys.indexes AS i
WHERE i.object_id IN (OBJECT_ID(N'dbo.BOOK'), OBJECT_ID(N'dbo.BOOK_COPY'),
                       OBJECT_ID(N'dbo.LOAN'), OBJECT_ID(N'dbo.RESERVATION'),
                       OBJECT_ID(N'dbo.READER_NOTIFICATION'), OBJECT_ID(N'dbo.NOTIFICATION_OUTBOX'))
  AND i.index_id > 0
ORDER BY TableName, IndexName;
GO
