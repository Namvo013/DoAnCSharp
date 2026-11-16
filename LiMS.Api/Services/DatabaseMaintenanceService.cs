using System.Data;
using LiMS.Api.Contracts;
using Microsoft.Data.SqlClient;

namespace LiMS.Api.Services;

public sealed class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly string _connectionString;
    private readonly string? _configuredBackupDirectory;
    private readonly ILogger<DatabaseMaintenanceService> _logger;
    private readonly string _databaseName;

    public DatabaseMaintenanceService(
        IConfiguration configuration,
        ILogger<DatabaseMaintenanceService> logger)
    {
        _connectionString = configuration.GetConnectionString("LiMS")
            ?? throw new InvalidOperationException("Connection string 'LiMS' is not configured.");
        _configuredBackupDirectory = configuration["DatabaseMaintenance:BackupDirectory"];
        _logger = logger;
        _databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(_databaseName))
            throw new InvalidOperationException("The LiMS connection string must specify a database.");
    }

    public async Task<DatabaseBackupStatusResponse> GetBackupStatusAsync(CancellationToken cancellationToken)
    {
        var directory = await GetBackupDirectoryAsync(cancellationToken);
        var files = await GetBackupFilesAsync(directory, cancellationToken);
        return new DatabaseBackupStatusResponse(_databaseName, directory, files);
    }

    public async Task<DatabaseBackupFileResponse> GetBackupAsync(
        string fileName,
        CancellationToken cancellationToken)
    {
        var directory = await GetBackupDirectoryAsync(cancellationToken);
        var backupPath = GetBackupFilePath(directory, fileName);
        return await GetBackupFileInfoAsync(backupPath, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tệp sao lưu đã chọn.");
    }

    public async Task<DatabaseBackupFileResponse> CreateBackupAsync(
        int actorId,
        CancellationToken cancellationToken)
    {
        var directory = await GetBackupDirectoryAsync(cancellationToken);
        var backup = await CreateBackupCoreAsync(directory, cancellationToken);
        _logger.LogInformation(
            "Admin account {AccountId} created database backup {BackupFile}.",
            actorId,
            backup.FileName);
        return backup;
    }

    public async Task<DatabaseRestoreResponse> RestoreBackupAsync(
        string fileName,
        string confirmDatabaseName,
        int actorId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(confirmDatabaseName, _databaseName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Xác nhận phục hồi phải trùng chính xác tên cơ sở dữ liệu.");

        var directory = await GetBackupDirectoryAsync(cancellationToken);
        var backupPath = GetBackupFilePath(directory, fileName);
        await VerifyBackupAsync(backupPath, cancellationToken);
        var safetyBackup = await CreateBackupCoreAsync(directory, cancellationToken);
        var databaseIdentifier = QuoteIdentifier(_databaseName);

        await using (var connection = await OpenMasterConnectionAsync(cancellationToken))
        {
            await ExecuteAsync(
                connection,
                $"ALTER DATABASE {databaseIdentifier} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;",
                null,
                cancellationToken);

            try
            {
                await ExecuteAsync(
                    connection,
                    $"RESTORE DATABASE {databaseIdentifier} FROM DISK = @backupPath WITH REPLACE, RECOVERY, CHECKSUM;",
                    backupPath,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is SqlException or OperationCanceledException)
            {
                try
                {
                    await ExecuteAsync(
                        connection,
                        $"ALTER DATABASE {databaseIdentifier} SET MULTI_USER WITH ROLLBACK IMMEDIATE;",
                        null,
                        CancellationToken.None);
                }
                catch (SqlException recoveryException)
                {
                    _logger.LogCritical(
                        recoveryException,
                        "Could not return database {DatabaseName} to multi-user mode after a restore failure.",
                        _databaseName);
                }

                throw;
            }

            try
            {
                await ExecuteAsync(
                    connection,
                    $"ALTER DATABASE {databaseIdentifier} SET MULTI_USER WITH ROLLBACK IMMEDIATE;",
                    null,
                    CancellationToken.None);
            }
            catch (SqlException exception)
            {
                _logger.LogCritical(
                    exception,
                    "Could not return database {DatabaseName} to multi-user mode after restore.",
                    _databaseName);
                throw;
            }
        }

        SqlConnection.ClearAllPools();
        var completedAt = DateTimeOffset.UtcNow;
        _logger.LogWarning(
            "Admin account {AccountId} restored database {DatabaseName} from {BackupFile}; safety backup is {SafetyBackupFile}.",
            actorId,
            _databaseName,
            fileName,
            safetyBackup.FileName);
        return new DatabaseRestoreResponse(_databaseName, fileName, safetyBackup.FileName, completedAt);
    }

    private async Task<DatabaseBackupFileResponse> CreateBackupCoreAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var fileName = $"LiMSDB_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.bak";
        var backupPath = Path.Combine(directory, fileName);
        var databaseIdentifier = QuoteIdentifier(_databaseName);
        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            $"BACKUP DATABASE {databaseIdentifier} TO DISK = @backupPath WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK = @backupPath WITH CHECKSUM;",
            backupPath,
            cancellationToken);
        return await GetBackupFileInfoAsync(backupPath, cancellationToken)
            ?? throw new InvalidOperationException("SQL Server completed the backup but did not record it in backup history.");
    }

    private async Task VerifyBackupAsync(string backupPath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await using (var headerCommand = new SqlCommand("RESTORE HEADERONLY FROM DISK = @backupPath;", connection))
        {
            AddBackupPath(headerCommand, backupPath);
            await using var reader = await headerCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new BusinessRuleException("Tệp sao lưu không chứa bản sao cơ sở dữ liệu.");
            var sourceDatabase = reader["DatabaseName"] as string;
            if (!string.Equals(sourceDatabase, _databaseName, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Tệp sao lưu không thuộc cơ sở dữ liệu LiMS hiện tại.");
        }

        await ExecuteAsync(
            connection,
            "RESTORE VERIFYONLY FROM DISK = @backupPath WITH CHECKSUM;",
            backupPath,
            cancellationToken);
    }

    private async Task<string> GetBackupDirectoryAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_configuredBackupDirectory))
            return Path.GetFullPath(_configuredBackupDirectory);

        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000));",
            connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string directory || string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException(
                "SQL Server không cung cấp thư mục sao lưu mặc định. Hãy cấu hình DatabaseMaintenance:BackupDirectory.");
        return Path.GetFullPath(directory);
    }

    private async Task<IReadOnlyList<DatabaseBackupFileResponse>> GetBackupFilesAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        var fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        const string query = """
            SELECT TOP (100)
                media.physical_device_name,
                backup.backup_size,
                backup.backup_finish_date
            FROM msdb.dbo.backupset AS backup
            INNER JOIN msdb.dbo.backupmediafamily AS media
                ON backup.media_set_id = media.media_set_id
            WHERE backup.database_name = @databaseName
                AND backup.type = 'D'
                AND backup.backup_finish_date IS NOT NULL
                AND media.device_type = 2
                AND LEFT(media.physical_device_name, LEN(@backupDirectory) + 1)
                    = @backupDirectory + NCHAR(92)
            ORDER BY backup.backup_finish_date DESC;
            """;

        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        AddBackupHistoryParameters(command, fullDirectory);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var backups = new List<DatabaseBackupFileResponse>();
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var path = reader.GetString(0);
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName)
                || !fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
                || !fileNames.Add(fileName))
                continue;

            var finishedAt = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Local);
            backups.Add(new DatabaseBackupFileResponse(
                fileName,
                Convert.ToInt64(reader.GetValue(1)),
                new DateTimeOffset(finishedAt)));
        }

        return backups;
    }

    private async Task<DatabaseBackupFileResponse?> GetBackupFileInfoAsync(
        string backupPath,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT TOP (1)
                backup.backup_size,
                backup.backup_finish_date
            FROM msdb.dbo.backupset AS backup
            INNER JOIN msdb.dbo.backupmediafamily AS media
                ON backup.media_set_id = media.media_set_id
            WHERE backup.database_name = @databaseName
                AND backup.type = 'D'
                AND backup.backup_finish_date IS NOT NULL
                AND media.device_type = 2
                AND media.physical_device_name = @backupPath
            ORDER BY backup.backup_finish_date DESC;
            """;

        await using var connection = await OpenMasterConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection);
        AddDatabaseNameParameter(command);
        AddBackupPath(command, backupPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var finishedAt = DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Local);
        return new DatabaseBackupFileResponse(
            Path.GetFileName(backupPath),
            Convert.ToInt64(reader.GetValue(0)),
            new DateTimeOffset(finishedAt));
    }

    private async Task<SqlConnection> OpenMasterConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };
        var connection = new SqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string commandText,
        string? backupPath,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(commandText, connection)
        {
            CommandTimeout = 0
        };
        if (backupPath is not null)
            AddBackupPath(command, backupPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddBackupPath(SqlCommand command, string backupPath) =>
        command.Parameters.Add("@backupPath", SqlDbType.NVarChar, 4000).Value = backupPath;

    private void AddBackupHistoryParameters(SqlCommand command, string directory)
    {
        AddDatabaseNameParameter(command);
        command.Parameters.Add("@backupDirectory", SqlDbType.NVarChar, 4000).Value = directory;
    }

    private void AddDatabaseNameParameter(SqlCommand command) =>
        command.Parameters.Add("@databaseName", SqlDbType.NVarChar, 128).Value = _databaseName;

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]")}]";

    private static string GetBackupFilePath(string directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            || !fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Tên tệp sao lưu không hợp lệ.");

        var fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(fullDirectory, fileName));
        if (!fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Tệp sao lưu phải nằm trong thư mục sao lưu được cấu hình.");
        return fullPath;
    }
}
