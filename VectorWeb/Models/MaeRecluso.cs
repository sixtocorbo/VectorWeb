using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class MaeRecluso
{
    public int IdRecluso { get; set; }

    public string NombreCompleto { get; set; } = null!;

    public bool? Activo { get; set; }

    public string? Documento { get; set; }

    public virtual ICollection<TraSalidasLaborale> TraSalidasLaborales { get; set; } = new List<TraSalidasLaborale>();
}
