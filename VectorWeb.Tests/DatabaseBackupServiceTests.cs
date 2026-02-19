using VectorWeb.Services;

namespace VectorWeb.Tests;

public class DatabaseBackupServiceTests
{
    [Fact]
    public void GetDatabaseName_ExtraeInitialCatalog()
    {
        const string connectionString = "Server=localhost;Database=SecretariaDB;Trusted_Connection=True;";

        var databaseName = DatabaseBackupService.GetDatabaseName(connectionString);

        Assert.Equal("SecretariaDB", databaseName);
    }

    [Fact]
    public void BuildBackupFileName_GeneraFormatoEsperado()
    {
        var timestamp = new DateTime(2026, 02, 19, 8, 30, 45);

        var fileName = DatabaseBackupService.BuildBackupFileName("Secretaria DB", timestamp);

        Assert.Equal("Secretaria_DB_20260219_083045.bak", fileName);
    }

    [Fact]
    public void EscapeIdentifier_EscapaCorchetes()
    {
        var escaped = DatabaseBackupService.EscapeIdentifier("DB]Prod");

        Assert.Equal("[DB]]Prod]", escaped);
    }
}
