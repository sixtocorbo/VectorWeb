/*
Objetivo:
  Acelerar la carga de /documentos después de confirmar pase.

Contexto:
  La bandeja consulta con filtros por IdOficinaActual y calcula:
  - COUNT DISTINCT de IdHiloConversacion
  - Conteos por EstadoSemaforo (columna calculada no determinística)
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
        FechaCreacion DESC
    )
    INCLUDE (IdDocumento, IdEstadoActual, IdDocumentoPadre, IdTipo, NumeroOficial, FechaVencimiento)
    WHERE IdEstadoActual <> 5;
END
GO

/*
NOTA:
  EstadoSemaforo usa GETDATE() en su definición, por lo tanto SQL Server la considera
  no determinística y no permite indexarla (ni en key, ni INCLUDE, ni estadísticas).
  Este índice usa FechaVencimiento para reducir lecturas en cálculos de semáforo.
*/

/*
Validación rápida sugerida:
SET STATISTICS IO, TIME ON;
-- ejecutar consulta de bandeja y comparar antes/después
SET STATISTICS IO, TIME OFF;
*/
