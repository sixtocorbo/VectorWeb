using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CatOficina
{
    public int IdOficina { get; set; }

    public string Nombre { get; set; } = null!;

    public bool? EsExterna { get; set; }

    public string? Direccion { get; set; }

    public virtual ICollection<CatUsuario> CatUsuarios { get; set; } = new List<CatUsuario>();

    public virtual ICollection<MaeDocumento> MaeDocumentos { get; set; } = new List<MaeDocumento>();

    public virtual ICollection<MaeNumeracionBitacora> MaeNumeracionBitacoras { get; set; } = new List<MaeNumeracionBitacora>();

    public virtual ICollection<MaeNumeracionRango> MaeNumeracionRangos { get; set; } = new List<MaeNumeracionRango>();

    public virtual ICollection<TraMovimiento> TraMovimientoIdOficinaDestinoNavigations { get; set; } = new List<TraMovimiento>();

    public virtual ICollection<TraMovimiento> TraMovimientoIdOficinaOrigenNavigations { get; set; } = new List<TraMovimiento>();
}
