using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;

namespace VectorWeb.Services;

public sealed class DocumentosService
{
    private readonly IDbContextFactory<SecretariaDbContext> _contextFactory;
    private readonly ILogger<DocumentosService> _logger;

    public DocumentosService(IDbContextFactory<SecretariaDbContext> contextFactory, ILogger<DocumentosService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<OperacionResultado> EliminarDocumentoTotalAsync(long idDocumento, int idUsuarioResponsable)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();

            var sql = @"
BEGIN TRY
    BEGIN TRAN;

    ;WITH CTE_Docs AS (
        SELECT IdDocumento FROM Mae_Documento WHERE IdDocumento = @IdDocumento
        UNION ALL
        SELECT d.IdDocumento
        FROM Mae_Documento d
        INNER JOIN CTE_Docs c ON d.IdDocumentoPadre = c.IdDocumento
    )
    SELECT IdDocumento
    INTO #DocsAEliminar
    FROM CTE_Docs;

    DELETE FROM Tra_SalidasLaboralesDocumentoRespaldo
    WHERE IdDocumento IN (SELECT IdDocumento FROM #DocsAEliminar);

    UPDATE Tra_SalidasLaborales
    SET IdDocumentoRespaldo = NULL
    WHERE IdDocumentoRespaldo IN (SELECT IdDocumento FROM #DocsAEliminar);

    DELETE FROM Tra_Movimiento
    WHERE IdDocumento IN (SELECT IdDocumento FROM #DocsAEliminar);

    DELETE FROM Tra_AdjuntoDocumento
    WHERE IdDocumento IN (SELECT IdDocumento FROM #DocsAEliminar);

    UPDATE Mae_Documento
    SET IdDocumentoPadre = NULL
    WHERE IdDocumento IN (SELECT IdDocumento FROM #DocsAEliminar);

    DELETE FROM Mae_Documento
    WHERE IdDocumento IN (SELECT IdDocumento FROM #DocsAEliminar);

    DROP TABLE #DocsAEliminar;

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
END CATCH";

            await context.Database.ExecuteSqlRawAsync(sql, new SqlParameter("@IdDocumento", idDocumento));

            context.EventosSistemas.Add(new EventosSistema
            {
                UsuarioId = idUsuarioResponsable,
                FechaEvento = DateTime.Now,
                Modulo = "DOCUMENTOS",
                Descripcion = $"Eliminación total en cascada del documento ID: {idDocumento} y sus documentos hijos/vinculados."
            });

            await context.SaveChangesAsync();
            return OperacionResultado.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico al eliminar el documento completo. ID: {IdDocumento}", idDocumento);
            return OperacionResultado.Fail($"Error al eliminar el documento en la base de datos: {ex.Message}");
        }
    }
}
