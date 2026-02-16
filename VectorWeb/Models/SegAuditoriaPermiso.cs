using System;

namespace VectorWeb.Models;

public partial class SegAuditoriaPermiso
{
    public long IdAuditoria { get; set; }

    public int? UsuarioId { get; set; }

    public string? UsuarioNombre { get; set; }

    public string? Rol { get; set; }

    public string PermisoRequerido { get; set; } = null!;

    public string? Modulo { get; set; }

    public string? Ruta { get; set; }

    public DateTime FechaEvento { get; set; }
}
