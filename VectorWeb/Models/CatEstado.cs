using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CatEstado
{
    public int IdEstado { get; set; }

    public string Nombre { get; set; } = null!;

    public string? ColorHex { get; set; }

    public virtual ICollection<MaeDocumento> MaeDocumentos { get; set; } = new List<MaeDocumento>();

    public virtual ICollection<TraMovimiento> TraMovimientos { get; set; } = new List<TraMovimiento>();
}
