using LiMS.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LiMS.Tests;

public sealed class DatabaseMaintenanceServiceTests
{
    [Fact]
    public void Constructor_requires_database_name_in_connection_string()
    {
        var configuration = CreateConfiguration("Server=localhost;Trusted_Connection=True");

        Assert.Throws<InvalidOperationException>(() =>
            new DatabaseMaintenanceService(configuration, NullLogger<DatabaseMaintenanceService>.Instance));
    }

    [Theory]
    [InlineData("../outside.bak")]
    [InlineData("..\\outside.bak")]
    [InlineData("subfolder/backup.bak")]
    [InlineData("")]
    public async Task GetBackupAsync_rejects_path_components_and_empty_names(string fileName)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetBackupAsync(fileName, CancellationToken.None));
    }

    [Theory]
    [InlineData("backup.txt")]
    [InlineData("backup.bak.tmp")]
    public async Task GetBackupAsync_accepts_only_bak_extension(string fileName)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetBackupAsync(fileName, CancellationToken.None));
    }

    [Fact]
    public async Task RestoreBackupAsync_requires_exact_database_confirmation_before_file_access()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RestoreBackupAsync(
                "backup.bak", "OtherDatabase", 12, CancellationToken.None));
    }

    [Fact]
    public async Task RestoreBackupAsync_rejects_path_traversal_before_sql_connection()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RestoreBackupAsync(
                "..\\outside.bak", "LiMSDB", 12, CancellationToken.None));
    }

    private static DatabaseMaintenanceService CreateService(string? backupDirectory = null)
    {
        var connectionString = "Server=localhost;Database=LiMSDB;Trusted_Connection=True;TrustServerCertificate=True";
        var directory = backupDirectory ?? Path.GetTempPath();
        var configuration = CreateConfiguration(connectionString, directory);
        return new DatabaseMaintenanceService(
            configuration,
            NullLogger<DatabaseMaintenanceService>.Instance);
    }

    private static IConfiguration CreateConfiguration(string connectionString, string? backupDirectory = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:LiMS"] = connectionString
        };
        if (backupDirectory is not null)
            settings["DatabaseMaintenance:BackupDirectory"] = backupDirectory;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
