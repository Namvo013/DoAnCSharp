using System.Data.Common;
using Xunit;

namespace LiMS.Tests;

/// <summary>Chỉ bật test SQL Server khi được cấp chuỗi kết nối tới database tên Test.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public SqlServerIntegrationFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable("LIMS_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = "Đặt LIMS_TEST_SQLSERVER tới database SQL Server dùng riêng cho test.";
            return;
        }

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var database = ReadDatabaseName(builder);
            if (string.IsNullOrWhiteSpace(database)
                || !database.Contains("test", StringComparison.OrdinalIgnoreCase))
            {
                Skip = "Test SQL Server chỉ chạy với database có tên chứa 'test'.";
            }
        }
        catch (ArgumentException)
        {
            Skip = "LIMS_TEST_SQLSERVER không phải connection string hợp lệ.";
        }
    }

    private static string? ReadDatabaseName(DbConnectionStringBuilder builder)
    {
        foreach (string key in builder.Keys)
        {
            if (key.Equals("Initial Catalog", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                return builder[key]?.ToString();
            }
        }

        return null;
    }
}
