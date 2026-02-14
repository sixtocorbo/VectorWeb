using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace VectorWeb.Models;

public partial class SecretariaDbContext : DbContext
{
    public SecretariaDbContext()
    {
    }

    public SecretariaDbContext(DbContextOptions<SecretariaDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<CatEstado> CatEstados { get; set; }

    public virtual DbSet<CatOficina> CatOficinas { get; set; }

    public virtual DbSet<CatOficinaBackup20260203093509> CatOficinaBackup20260203093509s { get; set; }

    public virtual DbSet<CatOficinaMergeLog> CatOficinaMergeLogs { get; set; }

    public virtual DbSet<CatTipoDocumento> CatTipoDocumentos { get; set; }

    public virtual DbSet<CatUsuario> CatUsuarios { get; set; }

    public virtual DbSet<CfgSistemaParametro> CfgSistemaParametros { get; set; }

    public virtual DbSet<CfgTiemposRespuestaHistorial> CfgTiemposRespuestaHistorials { get; set; }

    public virtual DbSet<CfgTiemposRespuestum> CfgTiemposRespuesta { get; set; }

    public virtual DbSet<EventosSistema> EventosSistemas { get; set; }

    public virtual DbSet<MaeCuposSecretarium> MaeCuposSecretaria { get; set; }

    public virtual DbSet<MaeDocumento> MaeDocumentos { get; set; }

    public virtual DbSet<MaeNumeracionRango> MaeNumeracionRangos { get; set; }

    public virtual DbSet<MaeRecluso> MaeReclusos { get; set; }

    public virtual DbSet<TraAdjuntoDocumento> TraAdjuntoDocumentos { get; set; }

    public virtual DbSet<TraMovimiento> TraMovimientos { get; set; }

    public virtual DbSet<TraSalidasLaborale> TraSalidasLaborales { get; set; }

    public virtual DbSet<TraSalidasLaboralesDocumentoRespaldo> TraSalidasLaboralesDocumentoRespaldos { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=.;Database=SecretariaDB;Trusted_Connection=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatEstado>(entity =>
        {
            entity.HasKey(e => e.IdEstado).HasName("PK__Cat_Esta__FBB0EDC179650C1B");

            entity.ToTable("Cat_Estado");

            entity.Property(e => e.ColorHex)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CatOficina>(entity =>
        {
            entity.HasKey(e => e.IdOficina).HasName("PK__Cat_Ofic__814E8052F033CC60");

            entity.ToTable("Cat_Oficina");

            entity.HasIndex(e => e.Nombre, "IX_Cat_Oficina_Nombre");

            entity.Property(e => e.Direccion)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.EsExterna).HasDefaultValue(false);
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CatOficinaBackup20260203093509>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("Cat_Oficina_Backup_20260203_093509");

            entity.Property(e => e.Direccion)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.IdOficina).ValueGeneratedOnAdd();
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CatOficinaMergeLog>(entity =>
        {
            entity.HasKey(e => e.IdLog).HasName("PK__Cat_Ofic__0C54DBC6C4A65755");

            entity.ToTable("Cat_Oficina_MergeLog");

            entity.Property(e => e.Fecha)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NombreFrom)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NombreTo)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Reason)
                .HasMaxLength(200)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CatTipoDocumento>(entity =>
        {
            entity.HasKey(e => e.IdTipo).HasName("PK__Cat_Tipo__9E3A29A58F8B342C");

            entity.ToTable("Cat_TipoDocumento");

            entity.Property(e => e.Codigo)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.EsInterno).HasDefaultValue(true);
            entity.Property(e => e.Nombre)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<CatUsuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK__Cat_Usua__5B65BF9714FC2FF2");

            entity.ToTable("Cat_Usuario");

            entity.HasIndex(e => e.IdOficina, "IX_Cat_Usuario_IdOficina");

            entity.HasIndex(e => e.UsuarioLogin, "UQ__Cat_Usua__F96234F39406CA8E").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Clave)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NombreCompleto)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Rol)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasDefaultValue("OPERADOR");
            entity.Property(e => e.UsuarioLogin)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.IdOficinaNavigation).WithMany(p => p.CatUsuarios)
                .HasForeignKey(d => d.IdOficina)
                .HasConstraintName("FK_Usuario_Oficina");
        });

        modelBuilder.Entity<CfgSistemaParametro>(entity =>
        {
            entity.HasKey(e => e.IdParametro);

            entity.ToTable("Cfg_SistemaParametros");

            entity.HasIndex(e => e.Clave, "UQ_Cfg_SistemaParametros_Clave").IsUnique();

            entity.Property(e => e.Clave).HasMaxLength(150);
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.FechaActualizacion)
                .HasPrecision(0)
                .HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.UsuarioActualizacion).HasMaxLength(100);
            entity.Property(e => e.Valor).HasMaxLength(500);
        });

        modelBuilder.Entity<CfgTiemposRespuestaHistorial>(entity =>
        {
            entity.HasKey(e => e.IdHist).HasName("PK__Cfg_Tiem__50E8A89D42D9D753");

            entity.ToTable("Cfg_TiemposRespuesta_Historial");

            entity.Property(e => e.FechaCambio)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Prioridad)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UsuarioSistema)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValueSql("(suser_sname())");
        });

        modelBuilder.Entity<CfgTiemposRespuestum>(entity =>
        {
            entity.HasKey(e => e.IdConfig).HasName("PK__Cfg_Tiem__79F21764E641B20C");

            entity.ToTable("Cfg_TiemposRespuesta", tb => tb.HasTrigger("trg_AuditarCambiosTiempos"));

            entity.Property(e => e.Prioridad)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.IdTipoDocumentoNavigation).WithMany(p => p.CfgTiemposRespuesta)
                .HasForeignKey(d => d.IdTipoDocumento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Cfg_Tiemp__IdTip__02FC7413");
        });

        modelBuilder.Entity<EventosSistema>(entity =>
        {
            entity.HasKey(e => e.IdEvento);

            entity.ToTable("EventosSistema");

            entity.HasIndex(e => e.FechaEvento, "IX_EventosSistema_Fecha");

            entity.HasIndex(e => new { e.Modulo, e.FechaEvento }, "IX_EventosSistema_Modulo_Fecha").IsDescending(false, true);

            entity.HasIndex(e => new { e.UsuarioId, e.FechaEvento }, "IX_EventosSistema_Usuario_Fecha").IsDescending(false, true);

            entity.Property(e => e.Descripcion)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.FechaEvento)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Modulo)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Usuario).WithMany(p => p.EventosSistemas)
                .HasForeignKey(d => d.UsuarioId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Eventos_CatUsuario");
        });

        modelBuilder.Entity<MaeCuposSecretarium>(entity =>
        {
            entity.HasKey(e => e.IdCupo).HasName("PK__Mae_Cupo__3E4BB16AD85A2B85");

            entity.ToTable("Mae_CuposSecretaria");

            entity.HasIndex(e => new { e.Fecha, e.IdCupo }, "IX_Mae_CuposSecretaria_Fecha_IdCupo").IsDescending();

            entity.HasIndex(e => new { e.IdTipo, e.Anio }, "UX_Mae_CuposSecretaria_Tipo_Anio").IsUnique();

            entity.Property(e => e.Anio)
                .HasDefaultValueSql("(datepart(year,getdate()))");

            entity.Property(e => e.Fecha).HasColumnType("datetime");

            entity.Property(e => e.NombreCupo)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.IdTipoNavigation).WithMany(p => p.MaeCuposSecretaria)
                .HasForeignKey(d => d.IdTipo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MaeCuposSecretaria_Tipo");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.MaeCuposSecretaria)
                .HasForeignKey(d => d.IdUsuario)
                .HasConstraintName("FK_MaeCuposSecretaria_Usuario");
        });

        modelBuilder.Entity<MaeDocumento>(entity =>
        {
            entity.HasKey(e => e.IdDocumento).HasName("PK__Mae_Docu__E52073471EC89580");

            entity.ToTable("Mae_Documento", tb => tb.HasTrigger("trg_AsignarVencimiento"));

            entity.HasIndex(e => new { e.IdOficinaActual, e.IdEstadoActual, e.FechaCreacion }, "IX_Bandeja_Rapida_Optimizado").IsDescending(false, false, true);

            entity.HasIndex(e => e.Asunto, "IX_Busqueda_Asunto");

            entity.HasIndex(e => new { e.IdDocumentoPadre, e.IdEstadoActual }, "IX_Documento_Padre_Estado").HasFilter("([IdDocumentoPadre] IS NOT NULL)");

            entity.HasIndex(e => e.IdTipo, "IX_FK_Mae_Documento_Tipo");

            entity.HasIndex(e => e.IdHiloConversacion, "IX_HiloConversacion");

            entity.HasIndex(e => e.FechaCreacion, "IX_Mae_Documento_Activos_Fecha_Global")
                .IsDescending()
                .HasFilter("([IdEstadoActual]<>(5))");

            entity.HasIndex(e => e.FechaCreacion, "IX_Mae_Documento_FechaCreacion").IsDescending();

            entity.HasIndex(e => new { e.IdHiloConversacion, e.IdOficinaActual, e.IdEstadoActual, e.FechaCreacion }, "IX_Mae_Documento_Hilo_Covering").IsDescending(false, false, false, true);

            entity.HasIndex(e => e.IdUsuarioCreador, "IX_Mae_Documento_UsuarioCreador");

            entity.HasIndex(e => e.NumeroOficial, "IX_NumeroOficial");

            entity.Property(e => e.Asunto)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.EstadoSemaforo)
                .HasMaxLength(8)
                .IsUnicode(false)
                .HasComputedColumnSql("(case when [FechaVencimiento] IS NULL then 'GRIS' when getdate()>[FechaVencimiento] then 'ROJO' when datediff(day,getdate(),[FechaVencimiento])<=(2) then 'AMARILLO' else 'VERDE' end)", false);
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FechaRecepcion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.FechaVencimiento).HasColumnType("datetime");
            entity.Property(e => e.Fojas).HasDefaultValue(1);
            entity.Property(e => e.IdHiloConversacion).HasDefaultValueSql("(newid())");
            entity.Property(e => e.NumeroInterno)
                .HasMaxLength(37)
                .IsUnicode(false)
                .HasComputedColumnSql("(concat([IdDocumento],'/',datepart(year,[FechaCreacion])))", false);
            entity.Property(e => e.NumeroOficial)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.IdDocumentoPadreNavigation).WithMany(p => p.InverseIdDocumentoPadreNavigation)
                .HasForeignKey(d => d.IdDocumentoPadre)
                .HasConstraintName("FK__Mae_Docum__IdDoc__32E0915F");

            entity.HasOne(d => d.IdEstadoActualNavigation).WithMany(p => p.MaeDocumentos)
                .HasForeignKey(d => d.IdEstadoActual)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Mae_Docum__IdEst__34C8D9D1");

            entity.HasOne(d => d.IdOficinaActualNavigation).WithMany(p => p.MaeDocumentos)
                .HasForeignKey(d => d.IdOficinaActual)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Mae_Docum__IdOfi__35BCFE0A");

            entity.HasOne(d => d.IdTipoNavigation).WithMany(p => p.MaeDocumentos)
                .HasForeignKey(d => d.IdTipo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Mae_Docum__IdTip__2F10007B");

            entity.HasOne(d => d.IdUsuarioCreadorNavigation).WithMany(p => p.MaeDocumentos)
                .HasForeignKey(d => d.IdUsuarioCreador)
                .HasConstraintName("FK__Mae_Docum__IdUsu__36B12243");
        });

        modelBuilder.Entity<MaeNumeracionRango>(entity =>
        {
            entity.HasKey(e => e.IdRango).HasName("PK__Mae_Nume__B9E65D7F1BA7E5ED");

            entity.ToTable("Mae_NumeracionRangos");

            entity.HasIndex(e => new { e.IdTipo, e.Anio, e.IdOficina, e.Activo }, "IX_Mae_NumeracionRangos_Completo");

            entity.HasIndex(e => new { e.IdTipo, e.Anio, e.IdOficina }, "UX_Mae_NumeracionRangos_Activo_OficinaTipoAnio")
                .IsUnique()
                .HasFilter("([Activo]=(1))");

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Anio)
                .HasDefaultValueSql("(datepart(year,getdate()))");
            entity.Property(e => e.FechaCreacion)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NombreRango)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.NumeroInicio).HasDefaultValue(1);

            entity.HasOne(d => d.IdOficinaNavigation).WithMany(p => p.MaeNumeracionRangos)
                .HasForeignKey(d => d.IdOficina)
                .HasConstraintName("FK_Numeracion_Oficina");

            entity.HasOne(d => d.IdTipoNavigation).WithMany(p => p.MaeNumeracionRangos)
                .HasForeignKey(d => d.IdTipo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Numeracion_Tipo");
        });

        modelBuilder.Entity<MaeRecluso>(entity =>
        {
            entity.HasKey(e => e.IdRecluso).HasName("PK__Mae_Recl__EDD18872EC740D52");

            entity.ToTable("Mae_Reclusos");

            entity.HasIndex(e => e.NombreCompleto, "IX_Busqueda_Reclusos").HasFilter("([Activo]=(1))");

            entity.HasIndex(e => e.Documento, "IX_Mae_Reclusos_Documento");

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Documento)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.NombreCompleto)
                .HasMaxLength(200)
                .IsUnicode(false);
        });

        modelBuilder.Entity<TraAdjuntoDocumento>(entity =>
        {
            entity.HasKey(e => e.IdAdjunto).HasName("PK__Tra_Adju__37A0A61F3D5F4959");

            entity.ToTable("Tra_AdjuntoDocumento");

            entity.HasIndex(e => new { e.IdDocumento, e.AddedAt }, "IX_Tra_AdjuntoDocumento_Listado");

            entity.HasIndex(e => new { e.IdDocumento, e.StoredName }, "UX_Tra_AdjuntoDocumento_Documento_StoredName").IsUnique();

            entity.Property(e => e.AddedAt).HasColumnType("datetime");
            entity.Property(e => e.DisplayName).HasMaxLength(255);
            entity.Property(e => e.StoredName).HasMaxLength(200);

            entity.HasOne(d => d.IdDocumentoNavigation).WithMany(p => p.TraAdjuntoDocumentos)
                .HasForeignKey(d => d.IdDocumento)
                .HasConstraintName("FK_Tra_AdjuntoDocumento_Mae_Documento");
        });

        modelBuilder.Entity<TraMovimiento>(entity =>
        {
            entity.HasKey(e => e.IdMovimiento).HasName("PK__Tra_Movi__881A6AE035EA50ED");

            entity.ToTable("Tra_Movimiento");

            entity.HasIndex(e => new { e.FechaMovimiento, e.IdUsuarioResponsable, e.IdOficinaOrigen, e.IdOficinaDestino }, "IX_Tra_Movimiento_Auditoria").IsDescending(true, false, false, false);

            entity.HasIndex(e => e.IdDocumento, "IX_Tra_Movimiento_Documento");

            entity.HasIndex(e => e.FechaMovimiento, "IX_Tra_Movimiento_Fecha");

            entity.HasIndex(e => new { e.IdOficinaDestino, e.FechaMovimiento }, "IX_Tra_Movimiento_OficinaDestino_Fecha").IsDescending(false, true);

            entity.HasIndex(e => new { e.IdOficinaOrigen, e.FechaMovimiento }, "IX_Tra_Movimiento_OficinaOrigen_Fecha").IsDescending(false, true);

            entity.HasIndex(e => new { e.IdDocumento, e.IdMovimiento }, "IX_Tra_Movimiento_Ultimo").IsDescending(false, true);

            entity.Property(e => e.FechaMovimiento)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ObservacionPase).HasMaxLength(500);

            entity.HasOne(d => d.IdDocumentoNavigation).WithMany(p => p.TraMovimientos)
                .HasForeignKey(d => d.IdDocumento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Tra_Movim__IdDoc__398D8EEE");

            entity.HasOne(d => d.IdEstadoEnEseMomentoNavigation).WithMany(p => p.TraMovimientos)
                .HasForeignKey(d => d.IdEstadoEnEseMomento)
                .HasConstraintName("FK__Tra_Movim__IdEst__3E52440B");

            entity.HasOne(d => d.IdOficinaDestinoNavigation).WithMany(p => p.TraMovimientoIdOficinaDestinoNavigations)
                .HasForeignKey(d => d.IdOficinaDestino)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Tra_Movim__IdOfi__3C69FB99");

            entity.HasOne(d => d.IdOficinaOrigenNavigation).WithMany(p => p.TraMovimientoIdOficinaOrigenNavigations)
                .HasForeignKey(d => d.IdOficinaOrigen)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Tra_Movim__IdOfi__3B75D760");

            entity.HasOne(d => d.IdUsuarioResponsableNavigation).WithMany(p => p.TraMovimientos)
                .HasForeignKey(d => d.IdUsuarioResponsable)
                .HasConstraintName("FK__Tra_Movim__IdUsu__3D5E1FD2");
        });

        modelBuilder.Entity<TraSalidasLaborale>(entity =>
        {
            entity.HasKey(e => e.IdSalida);

            entity.ToTable("Tra_SalidasLaborales");

            entity.HasIndex(e => new { e.IdRecluso, e.Activo, e.FechaVencimiento }, "IX_Tra_SalidasLaborales_Recluso_Activo_Vencimiento");

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.DescripcionAutorizacion).HasMaxLength(500);
            entity.Property(e => e.DetalleCustodia)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.FechaInicio).HasColumnType("datetime");
            entity.Property(e => e.FechaNotificacionJuez).HasColumnType("datetime");
            entity.Property(e => e.FechaVencimiento).HasColumnType("datetime");
            entity.Property(e => e.Horario)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.LugarTrabajo)
                .HasMaxLength(200)
                .IsUnicode(false);
            entity.Property(e => e.Observaciones)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.HasOne(d => d.IdDocumentoRespaldoNavigation).WithMany(p => p.TraSalidasLaborales)
                .HasForeignKey(d => d.IdDocumentoRespaldo)
                .HasConstraintName("FK_Salidas_Documento");

            entity.HasOne(d => d.IdReclusoNavigation).WithMany(p => p.TraSalidasLaborales)
                .HasForeignKey(d => d.IdRecluso)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Salidas_Reclusos");
        });

        modelBuilder.Entity<TraSalidasLaboralesDocumentoRespaldo>(entity =>
        {
            entity.HasKey(e => e.IdSalidaDocumentoRespaldo);

            entity.ToTable("Tra_SalidasLaboralesDocumentoRespaldo");

            entity.HasIndex(e => e.IdDocumento, "IX_Tra_SalidasLaboralesDocumentoRespaldo_IdDocumento");

            entity.HasIndex(e => e.IdSalida, "IX_Tra_SalidasLaboralesDocumentoRespaldo_IdSalida");

            entity.HasIndex(e => new { e.IdSalida, e.IdDocumento }, "UQ_Tra_SalidasLaboralesDocumentoRespaldo").IsUnique();

            entity.Property(e => e.FechaRegistro)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.IdDocumentoNavigation).WithMany(p => p.TraSalidasLaboralesDocumentoRespaldos)
                .HasForeignKey(d => d.IdDocumento)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tra_SalidasLaboralesDocumentoRespaldo_Mae_Documento");

            entity.HasOne(d => d.IdSalidaNavigation).WithMany(p => p.TraSalidasLaboralesDocumentoRespaldos)
                .HasForeignKey(d => d.IdSalida)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Tra_SalidasLaboralesDocumentoRespaldo_Tra_SalidasLaborales");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(e => e.NombreUsuario, "UQ_Usuarios_Nombre").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.Clave)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.NombreUsuario)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Rol)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
