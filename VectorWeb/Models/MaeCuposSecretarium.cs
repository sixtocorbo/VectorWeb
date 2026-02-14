using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class MaeCuposSecretarium
{
    public int IdCupo { get; set; }

    public string NombreCupo { get; set; } = null!;

    public int Anio { get; set; }

    public DateTime Fecha { get; set; }

    public int? IdUsuario { get; set; }

    public int IdTipo { get; set; }

    public int Cantidad { get; set; }

    public virtual CatTipoDocumento IdTipoNavigation { get; set; } = null!;

    public virtual CatUsuario? IdUsuarioNavigation { get; set; }
}
