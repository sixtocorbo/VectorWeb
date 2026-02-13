using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CfgSistemaParametro
{
    public int IdParametro { get; set; }

    public string Clave { get; set; } = null!;

    public string Valor { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string? UsuarioActualizacion { get; set; }

    public DateTime FechaActualizacion { get; set; }
}
