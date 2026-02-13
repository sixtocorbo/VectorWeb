using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class MaeNumeracionRango
{
    public int IdRango { get; set; }

    public int IdTipo { get; set; }

    public string NombreRango { get; set; } = null!;

    public int NumeroInicio { get; set; }

    public int NumeroFin { get; set; }

    public int UltimoUtilizado { get; set; }

    public bool Activo { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public int? IdOficina { get; set; }

    public int Anio { get; set; }

    public virtual CatOficina? IdOficinaNavigation { get; set; }

    public virtual CatTipoDocumento IdTipoNavigation { get; set; } = null!;
}
