namespace VectorWeb.Services.Security;

public static class AppPermissions
{
    public const string ClaimType = "permission";

    public static class Grupos
    {
        public const string Documentos = "Documentos";
        public const string Configuracion = "Configuración";
        public const string Seguridad = "Seguridad";
    }

    public sealed record PermisoDefinicion(string Valor, string Etiqueta, string Grupo);
    public sealed record PaginaDefinicion(string Nombre, string Ruta);

    public const string DocumentosVer = "documentos.ver";
    public const string DocumentosEditar = "documentos.editar";
    public const string VinculacionGestionar = "vinculacion.gestionar";
    public const string ReclusosGestionar = "reclusos.gestionar";
    public const string RenovacionesGestionar = "renovaciones.gestionar";
    public const string ConfiguracionCatalogos = "configuracion.catalogos";
    public const string ConfiguracionRangos = "configuracion.rangos";
    public const string SeguridadUsuariosRoles = "seguridad.usuarios_roles";

    public static readonly PermisoDefinicion[] Listado =
    [
        new(DocumentosVer, "Ver documentos", Grupos.Documentos),
        new(DocumentosEditar, "Crear/editar documentos", Grupos.Documentos),
        new(VinculacionGestionar, "Gestionar vinculación de documentos", Grupos.Documentos),
        new(ReclusosGestionar, "Gestionar reclusos", Grupos.Documentos),
        new(RenovacionesGestionar, "Gestionar renovaciones (Art. 120)", Grupos.Documentos),
        new(ConfiguracionCatalogos, "Configurar oficinas, tipos y estados", Grupos.Configuracion),
        new(ConfiguracionRangos, "Configurar rangos y cupos", Grupos.Configuracion),
        new(SeguridadUsuariosRoles, "Administrar usuarios, roles y permisos", Grupos.Seguridad)
    ];

    public static readonly string[] Todos = Listado.Select(x => x.Valor).ToArray();

    public static readonly IReadOnlyDictionary<string, string> Etiquetas = Listado
        .ToDictionary(x => x.Valor, x => x.Etiqueta, StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyDictionary<string, PaginaDefinicion[]> PaginasPorPermiso =
        new Dictionary<string, PaginaDefinicion[]>(StringComparer.OrdinalIgnoreCase)
        {
            [DocumentosVer] =
            [
                new("Dashboard", "/home"),
                new("Documentos", "/documentos"),
                new("Historial de documento", "/documentos/historial/{id}"),
                new("Recorrido de expediente", "/documentos/recorrido/{id}"),
                new("Guía", "/about")
            ],
            [DocumentosEditar] =
            [
                new("Nuevo documento", "/documentos/nuevo"),
                new("Editar documento", "/documentos/editar/{id}"),
                new("Realizar pase", "/documentos/pase/{id}")
            ],
            [VinculacionGestionar] =
            [
                new("Vinculación", "/documentos/vinculacion"),
                new("Vínculos de documento", "/documentos/vinculos/{idDocumento}")
            ],
            [ReclusosGestionar] =
            [
                new("Reclusos", "/reclusos"),
                new("Nuevo/editar recluso", "/reclusos/nuevo y /reclusos/editar/{id}")
            ],
            [RenovacionesGestionar] =
            [
                new("Art. 120", "/renovaciones"),
                new("Editar renovación", "/renovaciones/editar/{id}")
            ],
            [ConfiguracionCatalogos] =
            [
                new("Oficinas", "/configuracion/oficinas"),
                new("Tipos de documento", "/configuracion/tipos-documento"),
                new("Estados", "/configuracion/estados")
            ],
            [ConfiguracionRangos] =
            [
                new("Rangos", "/configuracion/rangos"),
                new("Semáforos", "/configuracion/semaforos"),
                new("Plazos documentos", "/configuracion/plazos-documentos")
            ],
            [SeguridadUsuariosRoles] =
            [
                new("Usuarios y roles", "/configuracion/usuarios"),
                new("Auditoría de permisos", "/configuracion/auditoria-permisos"),
                new("Respaldo de base de datos", "/configuracion/respaldo-bd")
            ]
        };
}
