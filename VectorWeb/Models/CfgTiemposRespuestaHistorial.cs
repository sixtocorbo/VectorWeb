using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CfgTiemposRespuestaHistorial
{
    public int IdHist { get; set; }

    public int IdConfigOriginal { get; set; }

    public int IdTipoDocumento { get; set; }

    public string? Prioridad { get; set; }

    public int? DiasAnteriores { get; set; }

    public int? DiasNuevos { get; set; }

    public DateTime? FechaCambio { get; set; }

    public string? UsuarioSistema { get; set; }
}
