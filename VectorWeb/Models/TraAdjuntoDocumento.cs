using System;
using System.Collections.Generic;

namespace VectorWeb.Models;

public partial class TraAdjuntoDocumento
{
    public long IdAdjunto { get; set; }

    public long IdDocumento { get; set; }

    public string StoredName { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public DateTime AddedAt { get; set; }

    public byte[] Content { get; set; } = null!;

    public virtual MaeDocumento IdDocumentoNavigation { get; set; } = null!;
}
