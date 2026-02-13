using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class TraSalidasLaboralesDocumentoRespaldo
{
    public int IdSalidaDocumentoRespaldo { get; set; }

    public int IdSalida { get; set; }

    public long IdDocumento { get; set; }

    public DateTime FechaRegistro { get; set; }

    public virtual MaeDocumento IdDocumentoNavigation { get; set; } = null!;

    public virtual TraSalidasLaborale IdSalidaNavigation { get; set; } = null!;
}
