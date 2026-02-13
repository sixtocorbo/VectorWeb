using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CatTipoDocumento
{
    public int IdTipo { get; set; }

    public string Nombre { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public bool? EsInterno { get; set; }

    public virtual ICollection<CfgTiemposRespuestum> CfgTiemposRespuesta { get; set; } = new List<CfgTiemposRespuestum>();

    public virtual ICollection<MaeCuposSecretarium> MaeCuposSecretaria { get; set; } = new List<MaeCuposSecretarium>();

    public virtual ICollection<MaeDocumento> MaeDocumentos { get; set; } = new List<MaeDocumento>();

    public virtual ICollection<MaeNumeracionRango> MaeNumeracionRangos { get; set; } = new List<MaeNumeracionRango>();
}
