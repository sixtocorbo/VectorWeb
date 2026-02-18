/*
    Configuración base de plazos según Decreto 500/991 (Uruguay)
    - Plazos en días: hábiles (se resuelven por lógica en C#)
    - Plazos en meses/largos: corridos (de fecha a fecha)
*/

SET NOCOUNT ON;

DECLARE @IdTipoProvidencia INT;
DECLARE @IdTipoMemorando INT;
DECLARE @IdTipoVista INT;
DECLARE @IdTipoResolucion INT;

-- 1) PROVIDENCIA (Art. 115) - 3 días hábiles
IF NOT EXISTS (SELECT 1 FROM Cat_TipoDocumento WHERE Nombre = 'PROVIDENCIA')
BEGIN
    INSERT INTO Cat_TipoDocumento (Nombre, Codigo, EsInterno)
    VALUES ('PROVIDENCIA', 'PROV', 1);
END;

SELECT @IdTipoProvidencia = IdTipo
FROM Cat_TipoDocumento
WHERE Nombre = 'PROVIDENCIA';

IF EXISTS (
    SELECT 1
    FROM Cfg_TiemposRespuesta
    WHERE IdTipoDocumento = @IdTipoProvidencia AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL')
BEGIN
    UPDATE Cfg_TiemposRespuesta
    SET DiasMaximos = 3,
        HorasMaximas = NULL
    WHERE IdTipoDocumento = @IdTipoProvidencia
      AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL';
END
ELSE
BEGIN
    INSERT INTO Cfg_TiemposRespuesta (IdTipoDocumento, Prioridad, DiasMaximos, HorasMaximas)
    VALUES (@IdTipoProvidencia, 'NORMAL', 3, NULL);
END;

-- 2) INFORMES / MEMORANDO (Art. 75) - 10 días hábiles (normal), 48 hs (urgente)
SELECT @IdTipoMemorando = IdTipo
FROM Cat_TipoDocumento
WHERE Nombre = 'MEMORANDO';

IF @IdTipoMemorando IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM Cfg_TiemposRespuesta
        WHERE IdTipoDocumento = @IdTipoMemorando AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL')
    BEGIN
        UPDATE Cfg_TiemposRespuesta
        SET DiasMaximos = 10,
            HorasMaximas = NULL
        WHERE IdTipoDocumento = @IdTipoMemorando
          AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL';
    END
    ELSE
    BEGIN
        INSERT INTO Cfg_TiemposRespuesta (IdTipoDocumento, Prioridad, DiasMaximos, HorasMaximas)
        VALUES (@IdTipoMemorando, 'NORMAL', 10, NULL);
    END;

    IF EXISTS (
        SELECT 1
        FROM Cfg_TiemposRespuesta
        WHERE IdTipoDocumento = @IdTipoMemorando AND UPPER(LTRIM(RTRIM(Prioridad))) = 'URGENTE')
    BEGIN
        UPDATE Cfg_TiemposRespuesta
        SET DiasMaximos = 2,
            HorasMaximas = 48
        WHERE IdTipoDocumento = @IdTipoMemorando
          AND UPPER(LTRIM(RTRIM(Prioridad))) = 'URGENTE';
    END
    ELSE
    BEGIN
        INSERT INTO Cfg_TiemposRespuesta (IdTipoDocumento, Prioridad, DiasMaximos, HorasMaximas)
        VALUES (@IdTipoMemorando, 'URGENTE', 2, 48);
    END;
END;

-- 3) VISTA (Arts. 75/76) - 10 días hábiles
IF NOT EXISTS (SELECT 1 FROM Cat_TipoDocumento WHERE Nombre = 'VISTA')
BEGIN
    INSERT INTO Cat_TipoDocumento (Nombre, Codigo, EsInterno)
    VALUES ('VISTA', 'VIST', 0);
END;

SELECT @IdTipoVista = IdTipo
FROM Cat_TipoDocumento
WHERE Nombre = 'VISTA';

IF EXISTS (
    SELECT 1
    FROM Cfg_TiemposRespuesta
    WHERE IdTipoDocumento = @IdTipoVista AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL')
BEGIN
    UPDATE Cfg_TiemposRespuesta
    SET DiasMaximos = 10,
        HorasMaximas = NULL
    WHERE IdTipoDocumento = @IdTipoVista
      AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL';
END
ELSE
BEGIN
    INSERT INTO Cfg_TiemposRespuesta (IdTipoDocumento, Prioridad, DiasMaximos, HorasMaximas)
    VALUES (@IdTipoVista, 'NORMAL', 10, NULL);
END;

-- 4) Resolución final / peticiones (Art. 118) - 120 días corridos
SELECT @IdTipoResolucion = IdTipo
FROM Cat_TipoDocumento
WHERE Nombre = 'OFICIO';

IF @IdTipoResolucion IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM Cfg_TiemposRespuesta
        WHERE IdTipoDocumento = @IdTipoResolucion AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL')
    BEGIN
        UPDATE Cfg_TiemposRespuesta
        SET DiasMaximos = 120,
            HorasMaximas = NULL
        WHERE IdTipoDocumento = @IdTipoResolucion
          AND UPPER(LTRIM(RTRIM(Prioridad))) = 'NORMAL';
    END
    ELSE
    BEGIN
        INSERT INTO Cfg_TiemposRespuesta (IdTipoDocumento, Prioridad, DiasMaximos, HorasMaximas)
        VALUES (@IdTipoResolucion, 'NORMAL', 120, NULL);
    END;
END;
