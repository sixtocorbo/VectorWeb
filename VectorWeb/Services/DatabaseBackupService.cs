using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services;

public sealed class DatabaseBackupService
{
    private readonly IDbContextFactory<SecretariaDbContext> _contextFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(
        IDbContextFactory<SecretariaDbContext> contextFactory,
        IConfiguration config,
        ILogger<DatabaseBackupService> logger)
    {
        _contextFactory = contextFactory;
        _config = config;
        _logger = logger;
    }

    // Renombrado a CreateBackupAsync para que la página Razor lo reconozca
    public async Task<DatabaseBackupResult> CreateBackupAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var dbName = context.Database.GetDbConnection().Database;

        // Obtenemos la ruta de los parámetros o usamos una por defecto
        var carpetaBackup = _config["BackupPath"] ?? @"C:\BackupsVector";
        if (!Directory.Exists(carpetaBackup)) Directory.CreateDirectory(carpetaBackup);

        var fileName = $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
        var fullPath = Path.Combine(carpetaBackup, fileName);

        try
        {
            // Comando SQL para backup físico
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = @p0 WITH FORMAT, NAME = 'Full Backup of {dbName}';";
            await context.Database.ExecuteSqlRawAsync(sql, fullPath);

            _logger.LogInformation("Backup generado en {Ruta}", fullPath);

            return new DatabaseBackupResult
            {
                DatabaseName = dbName,
                BackupPath = fullPath,
                CreatedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo el respaldo de BD");
            throw new Exception($"Error en SQL Server: {ex.Message}");
        }
    }
}

// El "contrato" que le faltaba a tu página Razor
public sealed class DatabaseBackupResult
{
    public string DatabaseName { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}