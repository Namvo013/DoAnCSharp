using System.Data.Common;
using LiMS.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Tests;

public sealed class SqlServerSchemaIntegrationTests
{
    private const string LatestReaderMigration = "20261121195533_AddReaderCoveringIndexes";

    [SqlServerIntegrationFact]
    public async Task Dedicated_database_has_applied_reader_migrations()
    {
        await using var database = await OpenDatabaseAsync();
        var applied = await ScalarAsync<int>(database,
            "SELECT COUNT(*) FROM dbo.__LiMSMigrationsHistory WHERE MigrationId = @id",
            ("@id", LatestReaderMigration));

        Assert.Equal(1, applied);
    }

    [SqlServerIntegrationFact]
    public async Task Reader_integrity_constraints_are_enabled_and_trusted()
    {
        await using var database = await OpenDatabaseAsync();
        var count = await ScalarAsync<int>(database, """
            SELECT COUNT(*)
            FROM sys.check_constraints
            WHERE parent_object_id IN
                (OBJECT_ID(N'dbo.READER'), OBJECT_ID(N'dbo.RESERVATION'), OBJECT_ID(N'dbo.LOAN'))
              AND name IN
                (N'CK_READER_CardDateRange', N'CK_READER_BorrowLimit',
                 N'CK_RESERVATION_ExpiryAfterCreated', N'CK_LOAN_DueDateAfterBorrowDate',
                 N'CK_LOAN_RenewCount')
              AND is_disabled = 0 AND is_not_trusted = 0;
            """);

        Assert.Equal(5, count);
    }

    [SqlServerIntegrationFact]
    public async Task Reader_focused_indexes_have_the_expected_key_columns()
    {
        await using var database = await OpenDatabaseAsync();

        Assert.Equal(
            new[] { "ReaderId", "ReturnDate", "DueDate" },
            await IndexColumnsAsync(database, "IX_LOAN_ReaderId_ReturnDate_DueDate", included: false));
        Assert.Equal(
            new[] { "BookId", "Status", "ExpireAt", "CreatedAt" },
            await IndexColumnsAsync(database, "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt", included: false));
        Assert.Equal(
            new[] { "ReaderId", "IsRead", "CreatedAt" },
            await IndexColumnsAsync(database, "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt", included: false));
    }

    [SqlServerIntegrationFact]
    public async Task Reader_workload_indexes_cover_the_projected_columns()
    {
        await using var database = await OpenDatabaseAsync();

        Assert.Equal(
            new[] { "BorrowDate", "CopyId", "Status", "RenewCount" },
            await IndexColumnsAsync(database, "IX_LOAN_ReaderId_ReturnDate_DueDate", included: true));
        Assert.Equal(
            new[] { "ReaderId" },
            await IndexColumnsAsync(database, "IX_RESERVATION_BookId_Status_ExpireAt_CreatedAt", included: true));
        Assert.Equal(
            new[] { "NotificationId", "Title", "Type" },
            await IndexColumnsAsync(database, "IX_READER_NOTIFICATION_ReaderId_IsRead_CreatedAt", included: true));
    }

    [SqlServerIntegrationFact]
    public async Task Primary_foreign_and_unique_keys_are_present_on_reader_tables()
    {
        await using var database = await OpenDatabaseAsync();
        var primaryKeys = await ScalarAsync<int>(database, """
            SELECT COUNT(*) FROM sys.key_constraints
            WHERE type = 'PK' AND parent_object_id IN
                (OBJECT_ID(N'dbo.ACCOUNT'), OBJECT_ID(N'dbo.READER'), OBJECT_ID(N'dbo.BOOK'),
                 OBJECT_ID(N'dbo.BOOK_COPY'), OBJECT_ID(N'dbo.LOAN'), OBJECT_ID(N'dbo.RESERVATION'));
            """);
        var readerForeignKeys = await ScalarAsync<int>(database, """
            SELECT COUNT(*) FROM sys.foreign_keys
            WHERE parent_object_id IN
                (OBJECT_ID(N'dbo.READER'), OBJECT_ID(N'dbo.LOAN'), OBJECT_ID(N'dbo.RESERVATION'));
            """);
        var uniqueIdentifiers = await ScalarAsync<int>(database, """
            SELECT COUNT(*) FROM sys.indexes
            WHERE is_unique = 1 AND name IN
                (N'IX_ACCOUNT_Email', N'IX_ACCOUNT_Username', N'IX_READER_CardNo',
                 N'IX_BOOK_Isbn', N'IX_BOOK_COPY_Barcode');
            """);

        Assert.True(primaryKeys >= 6);
        Assert.True(readerForeignKeys >= 6);
        Assert.Equal(5, uniqueIdentifiers);
    }

    [SqlServerIntegrationFact]
    public async Task Open_copy_loan_has_a_filtered_unique_index()
    {
        await using var database = await OpenDatabaseAsync();
        var filter = await ScalarAsync<string?>(database, """
            SELECT filter_definition FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.LOAN') AND name = N'IX_LOAN_CopyId'
              AND is_unique = 1;
            """);

        Assert.NotNull(filter);
        Assert.Contains("ReturnDate", filter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IS NULL", filter, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerIntegrationFact]
    public async Task Notification_deduplication_keys_are_unique()
    {
        await using var database = await OpenDatabaseAsync();
        var uniqueIndexes = await ScalarAsync<int>(database, """
            SELECT COUNT(*) FROM sys.indexes
            WHERE is_unique = 1 AND name IN
                (N'IX_READER_NOTIFICATION_DeduplicationKey', N'IX_NOTIFICATION_OUTBOX_DeduplicationKey');
            """);

        Assert.Equal(2, uniqueIndexes);
    }

    [SqlServerIntegrationFact]
    public async Task Reader_reporting_views_and_procedures_are_installed()
    {
        await using var database = await OpenDatabaseAsync();
        var viewCount = await ScalarAsync<int>(database, """
            SELECT COUNT(*) FROM sys.views
            WHERE name IN (N'vw_ReaderPopularBooks', N'vw_ReaderLoanHistoryDetails',
                           N'vw_ReaderOverdueLoans', N'vw_ReaderReservationQueue');
            """);
        var procedureCount = await ScalarAsync<int>(database, """
            SELECT COUNT(*) FROM sys.procedures
            WHERE name IN (N'usp_Reader_GetPopularBooks', N'usp_Reader_GetLoanHistory',
                           N'usp_Reader_GetOverdueLoans', N'usp_Reader_GetBookAvailability');
            """);

        Assert.Equal(4, viewCount);
        Assert.Equal(4, procedureCount);
    }

    private static async Task<LiMSDbContext> OpenDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("LIMS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("SQL integration test was not skipped without its connection string.");

        var options = new DbContextOptionsBuilder<LiMSDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var context = new LiMSDbContext(options);
        await context.Database.OpenConnectionAsync();
        return context;
    }

    private static async Task<T> ScalarAsync<T>(
        LiMSDbContext context,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(context, sql, parameters);
        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
            return default!;
        return (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    private static async Task<string[]> IndexColumnsAsync(
        LiMSDbContext context,
        string indexName,
        bool included)
    {
        await using var command = CreateCommand(context, """
            SELECT c.name
            FROM sys.indexes AS i
            INNER JOIN sys.index_columns AS ic
                ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c
                ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.name = @name AND ic.is_included_column = @included
            ORDER BY CASE WHEN @included = 1 THEN ic.index_column_id ELSE ic.key_ordinal END;
            """, ("@name", indexName), ("@included", included));

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns.ToArray();
    }

    private static DbCommand CreateCommand(
        LiMSDbContext context,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}
