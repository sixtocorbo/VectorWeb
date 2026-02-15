using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class MaeDocumento
{
    public long IdDocumento { get; set; }

    public int IdTipo { get; set; }

    public string? NumeroOficial { get; set; }

    public string NumeroInterno { get; set; } = null!;

    public string Asunto { get; set; } = null!;

    public string? Descripcion { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public int? AnioDocumento { get; set; }

    public DateTime? FechaRecepcion { get; set; }

    public int? Fojas { get; set; }

    public long? IdDocumentoPadre { get; set; }

    public Guid IdHiloConversacion { get; set; }

    public int IdEstadoActual { get; set; }

    public int IdOficinaActual { get; set; }

    public int? IdUsuarioCreador { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public string EstadoSemaforo { get; set; } = null!;

    public virtual MaeDocumento? IdDocumentoPadreNavigation { get; set; }

    public virtual CatEstado IdEstadoActualNavigation { get; set; } = null!;

    public virtual CatOficina IdOficinaActualNavigation { get; set; } = null!;

    public virtual CatTipoDocumento IdTipoNavigation { get; set; } = null!;

    public virtual CatUsuario? IdUsuarioCreadorNavigation { get; set; }

    public virtual ICollection<MaeDocumento> InverseIdDocumentoPadreNavigation { get; set; } = new List<MaeDocumento>();

    public virtual ICollection<TraAdjuntoDocumento> TraAdjuntoDocumentos { get; set; } = new List<TraAdjuntoDocumento>();

    public virtual ICollection<TraMovimiento> TraMovimientos { get; set; } = new List<TraMovimiento>();

    public virtual ICollection<TraSalidasLaborale> TraSalidasLaborales { get; set; } = new List<TraSalidasLaborale>();

    public virtual ICollection<TraSalidasLaboralesDocumentoRespaldo> TraSalidasLaboralesDocumentoRespaldos { get; set; } = new List<TraSalidasLaboralesDocumentoRespaldo>();
}
