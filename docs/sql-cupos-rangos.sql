-- Script propuesto para soportar cupos anuales de Secretaría y control de rangos por oficina/tipo/año.
-- Ejecutar en SQL Server sobre SecretariaDB.

BEGIN TRANSACTION;

-- 1) Cupos de Secretaría por tipo y año.
IF COL_LENGTH('dbo.Mae_CuposSecretaria', 'NombreCupo') IS NULL
BEGIN
    ALTER TABLE dbo.Mae_CuposSecretaria
    ADD NombreCupo VARCHAR(100) NOT NULL CONSTRAINT DF_Mae_CuposSecretaria_NombreCupo DEFAULT ('CUPO-SIN-DEFINIR');
END;

IF COL_LENGTH('dbo.Mae_CuposSecretaria', 'Anio') IS NULL
BEGIN
    ALTER TABLE dbo.Mae_CuposSecretaria
    ADD Anio INT NOT NULL CONSTRAINT DF_Mae_CuposSecretaria_Anio DEFAULT (DATEPART(YEAR, GETDATE()));
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Mae_CuposSecretaria_Tipo_Anio'
      AND object_id = OBJECT_ID('dbo.Mae_CuposSecretaria')
)
BEGIN
    CREATE UNIQUE INDEX UX_Mae_CuposSecretaria_Tipo_Anio
        ON dbo.Mae_CuposSecretaria(IdTipo, Anio);
END;

-- 2) Rangos: año dinámico y unicidad de rango activo por tipo+oficina+año.
DECLARE @dfAnioRango SYSNAME;
SELECT @dfAnioRango = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c
    ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.Mae_NumeracionRangos')
  AND c.name = 'Anio';

IF @dfAnioRango IS NOT NULL
BEGIN
    EXEC('ALTER TABLE dbo.Mae_NumeracionRangos DROP CONSTRAINT ' + QUOTENAME(@dfAnioRango));
END;

ALTER TABLE dbo.Mae_NumeracionRangos
ADD CONSTRAINT DF_Mae_NumeracionRangos_Anio DEFAULT (DATEPART(YEAR, GETDATE())) FOR Anio;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Mae_NumeracionRangos_Activo_OficinaTipoAnio'
      AND object_id = OBJECT_ID('dbo.Mae_NumeracionRangos')
)
BEGIN
    CREATE UNIQUE INDEX UX_Mae_NumeracionRangos_Activo_OficinaTipoAnio
        ON dbo.Mae_NumeracionRangos(IdTipo, Anio, IdOficina)
        WHERE Activo = 1;
END;

COMMIT TRANSACTION;
