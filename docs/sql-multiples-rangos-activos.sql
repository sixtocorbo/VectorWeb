-- Permite múltiples rangos activos por combinación oficina/tipo/año.
-- Ejecutar en SQL Server sobre SecretariaDB.

BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_Mae_NumeracionRangos_Activo_OficinaTipoAnio'
      AND object_id = OBJECT_ID('dbo.Mae_NumeracionRangos')
)
BEGIN
    DROP INDEX UX_Mae_NumeracionRangos_Activo_OficinaTipoAnio ON dbo.Mae_NumeracionRangos;
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Mae_NumeracionRangos_Activo_OficinaTipoAnio'
      AND object_id = OBJECT_ID('dbo.Mae_NumeracionRangos')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Mae_NumeracionRangos_Activo_OficinaTipoAnio
        ON dbo.Mae_NumeracionRangos(IdTipo, Anio, IdOficina)
        WHERE Activo = 1;
END;

IF OBJECT_ID('dbo.TRG_Mae_NumeracionRangos_MaxDosActivos', 'TR') IS NOT NULL
BEGIN
    DROP TRIGGER dbo.TRG_Mae_NumeracionRangos_MaxDosActivos;
END;

COMMIT TRANSACTION;
