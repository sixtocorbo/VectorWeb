using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class CatOficinaMergeLog
{
    public int IdLog { get; set; }

    public DateTime? Fecha { get; set; }

    public int? IdFrom { get; set; }

    public int? IdTo { get; set; }

    public string? NombreFrom { get; set; }

    public string? NombreTo { get; set; }

    public string? Reason { get; set; }
}
