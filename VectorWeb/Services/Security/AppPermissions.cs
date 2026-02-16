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
}
