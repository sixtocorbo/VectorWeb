using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class MaeNumeracionBitacora
{
    public int IdBitacora { get; set; }

    public DateTime Fecha { get; set; }

    public string Entidad { get; set; } = null!;

    public string Accion { get; set; } = null!;

    public string Detalle { get; set; } = null!;

    public int IdTipo { get; set; }

    public int Anio { get; set; }

    public int? IdOficina { get; set; }

    public int? IdUsuario { get; set; }

    public int? IdReferencia { get; set; }

    public virtual CatOficina? IdOficinaNavigation { get; set; }

    public virtual CatTipoDocumento IdTipoNavigation { get; set; } = null!;

    public virtual CatUsuario? IdUsuarioNavigation { get; set; }
}
