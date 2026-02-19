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
        var backupDirectory = Path.GetFullPath(_backupDirectory);
        Directory.CreateDirectory(backupDirectory);

        var fileName = BuildBackupFileName(databaseName, DateTime.Now);
        var fullPath = Path.Combine(backupDirectory, fileName);

        var sql = $"BACKUP DATABASE {EscapeIdentifier(databaseName)} TO DISK = N'{EscapeLiteral(fullPath)}' WITH INIT, FORMAT, CHECKSUM, STATS = 10";

        var timeoutOriginal = dbContext.Database.GetCommandTimeout();
        dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        finally
        {
            dbContext.Database.SetCommandTimeout(timeoutOriginal);
        }

        return new DatabaseBackupResult(databaseName, fullPath, DateTime.Now);
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
