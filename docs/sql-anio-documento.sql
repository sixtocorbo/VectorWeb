BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Mae_Documento', 'AnioDocumento') IS NULL
BEGIN
    ALTER TABLE dbo.Mae_Documento
        ADD AnioDocumento INT NULL;
END;

UPDATE dbo.Mae_Documento
SET AnioDocumento = YEAR(FechaCreacion)
WHERE AnioDocumento IS NULL;

DECLARE @definition NVARCHAR(MAX);
SELECT @definition = cc.definition
FROM sys.computed_columns cc
JOIN sys.columns c ON c.object_id = cc.object_id AND c.column_id = cc.column_id
WHERE cc.object_id = OBJECT_ID('dbo.Mae_Documento')
  AND c.name = 'NumeroInterno';

IF @definition IS NULL OR @definition NOT LIKE '%AnioDocumento%'
BEGIN
    ALTER TABLE dbo.Mae_Documento DROP COLUMN NumeroInterno;

    ALTER TABLE dbo.Mae_Documento
        ADD NumeroInterno AS (concat([IdDocumento], '/', isnull([AnioDocumento], datepart(year, [FechaCreacion]))));
END;

COMMIT TRANSACTION;
