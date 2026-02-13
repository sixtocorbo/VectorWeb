using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CatUsuario
{
    public int IdUsuario { get; set; }

    public string NombreCompleto { get; set; } = null!;

    public string UsuarioLogin { get; set; } = null!;

    public bool? Activo { get; set; }

    public string? Clave { get; set; }

    public string? Rol { get; set; }

    public int? IdOficina { get; set; }

    public virtual ICollection<EventosSistema> EventosSistemas { get; set; } = new List<EventosSistema>();

    public virtual CatOficina? IdOficinaNavigation { get; set; }

    public virtual ICollection<MaeCuposSecretarium> MaeCuposSecretaria { get; set; } = new List<MaeCuposSecretarium>();

    public virtual ICollection<MaeDocumento> MaeDocumentos { get; set; } = new List<MaeDocumento>();

    public virtual ICollection<TraMovimiento> TraMovimientos { get; set; } = new List<TraMovimiento>();
}
