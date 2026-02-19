using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services;

public sealed class DatabaseBackupService
{
    private readonly IDbContextFactory<SecretariaDbContext> _dbFactory;
    private readonly string _backupDirectory;

    public DatabaseBackupService(IDbContextFactory<SecretariaDbContext> dbFactory, IConfiguration configuration)
    {
        _dbFactory = dbFactory;
        _backupDirectory = configuration["DatabaseBackup:Directory"] ?? "Backups";
    }

    public async Task<DatabaseBackupResult> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("No se encontró la cadena de conexión configurada.");
        }

        var databaseName = GetDatabaseName(connectionString);
        var fileName = BuildBackupFileName(databaseName, DateTime.Now);
        var preferredBackupPath = Path.Combine(Path.GetFullPath(_backupDirectory), fileName);

        var timeoutOriginal = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        string? sqlServerDefaultDirectory = null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preferredBackupPath)!);
            await ExecuteBackupAsync(dbContext, databaseName, preferredBackupPath, cancellationToken);

            return new DatabaseBackupResult(databaseName, preferredBackupPath, DateTime.Now);
        }
        catch (SqlException ex) when (IsAccessDenied(ex))
        {
            sqlServerDefaultDirectory = await GetSqlServerDefaultBackupDirectoryAsync(dbContext, cancellationToken);
            if (string.IsNullOrWhiteSpace(sqlServerDefaultDirectory))
            {
                throw;
            }

            var fallbackPath = Path.Combine(sqlServerDefaultDirectory, fileName);
            await ExecuteBackupAsync(dbContext, databaseName, fallbackPath, cancellationToken);

            var finalPath = TryCopyBackupToPreferredPath(fallbackPath, preferredBackupPath);

            return new DatabaseBackupResult(databaseName, finalPath, DateTime.Now);
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(timeoutOriginal);
        }
    }

    private static async Task ExecuteBackupAsync(SecretariaDbContext dbContext, string databaseName, string fullPath, CancellationToken cancellationToken)
    {
        var sql = $"BACKUP DATABASE {EscapeIdentifier(databaseName)} TO DISK = N'{EscapeLiteral(fullPath)}' WITH INIT, FORMAT, CHECKSUM, STATS = 10";
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private static bool IsAccessDenied(SqlException ex)
        => ex.Message.Contains("Operating system error 5", StringComparison.OrdinalIgnoreCase)
           || ex.Message.Contains("Acceso denegado", StringComparison.OrdinalIgnoreCase)
           || ex.Message.Contains("Access is denied", StringComparison.OrdinalIgnoreCase);

    private static string TryCopyBackupToPreferredPath(string sourcePath, string preferredBackupPath)
    {
        try
        {
            var targetDirectory = Path.GetDirectoryName(preferredBackupPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                return sourcePath;
            }

            Directory.CreateDirectory(targetDirectory);
            File.Copy(sourcePath, preferredBackupPath, overwrite: true);

            return preferredBackupPath;
        }
        catch (IOException)
        {
            return sourcePath;
        }
        catch (UnauthorizedAccessException)
        {
            return sourcePath;
        }
    }

    private static async Task<string?> GetSqlServerDefaultBackupDirectoryAsync(SecretariaDbContext dbContext, CancellationToken cancellationToken)
    {
        await using var connection = dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result as string;
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    public static string GetDatabaseName(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException("La cadena de conexión no contiene el nombre de la base de datos (Initial Catalog).");
        }

        return builder.InitialCatalog;
    }

    public static string BuildBackupFileName(string databaseName, DateTime timestamp)
    {
        var safeDatabaseName = databaseName.Replace(' ', '_');
        return $"{safeDatabaseName}_{timestamp:yyyyMMdd_HHmmss}.bak";
    }

    public static string EscapeIdentifier(string identifier)
        => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    public static string EscapeLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);
}

public sealed record DatabaseBackupResult(string DatabaseName, string BackupPath, DateTime CreatedAt);
