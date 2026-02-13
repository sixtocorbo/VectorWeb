using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CatOficinaBackup20260203093509
{
    public int IdOficina { get; set; }

    public string Nombre { get; set; } = null!;

    public bool? EsExterna { get; set; }

    public string? Direccion { get; set; }
}
