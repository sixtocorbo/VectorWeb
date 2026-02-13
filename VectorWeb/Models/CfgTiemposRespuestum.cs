using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CfgTiemposRespuestum
{
    public int IdConfig { get; set; }

    public int IdTipoDocumento { get; set; }

    public string Prioridad { get; set; } = null!;

    public int DiasMaximos { get; set; }

    public int? HorasMaximas { get; set; }

    public virtual CatTipoDocumento IdTipoDocumentoNavigation { get; set; } = null!;
}
