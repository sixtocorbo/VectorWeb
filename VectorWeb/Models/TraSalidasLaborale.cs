using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class TraSalidasLaborale
{
    public int IdSalida { get; set; }

    public int IdRecluso { get; set; }

    public string LugarTrabajo { get; set; } = null!;

    public DateTime FechaInicio { get; set; }

    public DateTime FechaVencimiento { get; set; }

    public long? IdDocumentoRespaldo { get; set; }

    public bool? Activo { get; set; }

    public string? Observaciones { get; set; }

    public string? Horario { get; set; }

    public string? DetalleCustodia { get; set; }

    public DateTime? FechaNotificacionJuez { get; set; }

    public string? DescripcionAutorizacion { get; set; }

    public virtual MaeDocumento? IdDocumentoRespaldoNavigation { get; set; }

    public virtual MaeRecluso IdReclusoNavigation { get; set; } = null!;

    public virtual ICollection<TraSalidasLaboralesDocumentoRespaldo> TraSalidasLaboralesDocumentoRespaldos { get; set; } = new List<TraSalidasLaboralesDocumentoRespaldo>();
}
