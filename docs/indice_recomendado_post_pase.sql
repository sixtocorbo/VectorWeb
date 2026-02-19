/*
Objetivo:
  Acelerar la carga de /documentos después de confirmar pase.

Contexto:
  La bandeja consulta con filtros por IdOficinaActual y calcula:
  - COUNT DISTINCT de IdHiloConversacion
  - Conteos por EstadoSemaforo
  - Ordenamiento por FechaCreacion DESC

Índice recomendado:
*/
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Mae_Documento_Oficina_Hilo_Semaforo_Fecha'
      AND object_id = OBJECT_ID('dbo.Mae_Documento')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Mae_Documento_Oficina_Hilo_Semaforo_Fecha
    ON dbo.Mae_Documento
    (
        IdOficinaActual ASC,
        IdHiloConversacion ASC,
        EstadoSemaforo ASC,
        FechaCreacion DESC
    )
    INCLUDE (IdDocumento, IdEstadoActual, IdDocumentoPadre, IdTipo, NumeroOficial)
    WHERE IdEstadoActual <> 5;
END
GO

/*
Validación rápida sugerida:
SET STATISTICS IO, TIME ON;
-- ejecutar consulta de bandeja y comparar antes/después
SET STATISTICS IO, TIME OFF;
*/
