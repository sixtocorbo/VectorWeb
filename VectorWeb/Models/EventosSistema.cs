using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class EventosSistema
{
    public long IdEvento { get; set; }

    public int UsuarioId { get; set; }

    public DateTime? FechaEvento { get; set; }

    public string? Descripcion { get; set; }

    public string? Modulo { get; set; }

    public virtual CatUsuario Usuario { get; set; } = null!;
}
