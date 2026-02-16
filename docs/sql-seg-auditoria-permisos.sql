/*
    Script para crear tabla de auditoría de accesos denegados por permiso.
    Ejecutar en la BD SecretariaDB antes de usar la pantalla de auditoría.
*/

IF OBJECT_ID('dbo.Seg_AuditoriaPermisos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Seg_AuditoriaPermisos
    (
        IdAuditoria       BIGINT IDENTITY(1,1) NOT NULL,
        UsuarioId         INT NULL,
        UsuarioNombre     VARCHAR(100) NULL,
        Rol               VARCHAR(50) NULL,
        PermisoRequerido  VARCHAR(150) NOT NULL,
        Modulo            VARCHAR(150) NULL,
        Ruta              VARCHAR(250) NULL,
        FechaEvento       DATETIME2(0) NOT NULL
            CONSTRAINT DF_Seg_AuditoriaPermisos_FechaEvento DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_Seg_AuditoriaPermisos PRIMARY KEY CLUSTERED (IdAuditoria ASC)
    );

    CREATE INDEX IX_Seg_AuditoriaPermisos_FechaEvento
        ON dbo.Seg_AuditoriaPermisos (FechaEvento DESC);

    CREATE INDEX IX_Seg_AuditoriaPermisos_Usuario_Fecha
        ON dbo.Seg_AuditoriaPermisos (UsuarioId ASC, FechaEvento DESC);
END
GO
