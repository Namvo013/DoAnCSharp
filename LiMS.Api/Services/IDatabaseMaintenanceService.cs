using LiMS.Api.Contracts;

namespace LiMS.Api.Services;

public interface IDatabaseMaintenanceService
{
    Task<DatabaseBackupStatusResponse> GetBackupStatusAsync(CancellationToken cancellationToken);
    Task<DatabaseBackupFileResponse> GetBackupAsync(string fileName, CancellationToken cancellationToken);
    Task<DatabaseBackupFileResponse> CreateBackupAsync(int actorId, CancellationToken cancellationToken);
    Task<DatabaseRestoreResponse> RestoreBackupAsync(
        string fileName,
        string confirmDatabaseName,
        int actorId,
        CancellationToken cancellationToken);
}
