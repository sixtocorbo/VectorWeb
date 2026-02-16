namespace VectorWeb.Services.Security;

public static class AppPermissions
{
    public const string ClaimType = "permission";

    public const string DocumentosVer = "documentos.ver";
    public const string DocumentosEditar = "documentos.editar";
    public const string VinculacionGestionar = "vinculacion.gestionar";
    public const string ReclusosGestionar = "reclusos.gestionar";
    public const string RenovacionesGestionar = "renovaciones.gestionar";
    public const string ConfiguracionCatalogos = "configuracion.catalogos";
    public const string ConfiguracionRangos = "configuracion.rangos";
    public const string SeguridadUsuariosRoles = "seguridad.usuarios_roles";

    public static readonly string[] Todos =
    [
        DocumentosVer,
        DocumentosEditar,
        VinculacionGestionar,
        ReclusosGestionar,
        RenovacionesGestionar,
        ConfiguracionCatalogos,
        ConfiguracionRangos,
        SeguridadUsuariosRoles
    ];

    public static readonly IReadOnlyDictionary<string, string> Etiquetas = new Dictionary<string, string>
    {
        [DocumentosVer] = "Ver documentos",
        [DocumentosEditar] = "Crear/editar documentos",
        [VinculacionGestionar] = "Gestionar vinculación de documentos",
        [ReclusosGestionar] = "Gestionar reclusos",
        [RenovacionesGestionar] = "Gestionar renovaciones (Art. 120)",
        [ConfiguracionCatalogos] = "Configurar oficinas, tipos y estados",
        [ConfiguracionRangos] = "Configurar rangos y cupos",
        [SeguridadUsuariosRoles] = "Administrar usuarios, roles y permisos"
    };
}
