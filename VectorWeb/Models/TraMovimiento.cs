using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class TraMovimiento
{
    public long IdMovimiento { get; set; }

    public long IdDocumento { get; set; }

    public DateTime? FechaMovimiento { get; set; }

    public int IdOficinaOrigen { get; set; }

    public int IdOficinaDestino { get; set; }

    public int? IdUsuarioResponsable { get; set; }

    public string? ObservacionPase { get; set; }

    public int? IdEstadoEnEseMomento { get; set; }

    public virtual MaeDocumento IdDocumentoNavigation { get; set; } = null!;

    public virtual CatEstado? IdEstadoEnEseMomentoNavigation { get; set; }

    public virtual CatOficina IdOficinaDestinoNavigation { get; set; } = null!;

    public virtual CatOficina IdOficinaOrigenNavigation { get; set; } = null!;

    public virtual CatUsuario? IdUsuarioResponsableNavigation { get; set; }
}
