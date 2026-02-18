# Auditoría rápida de permisos por página

Fecha: 2026-02-18

## Criterio revisado

Se verificó qué páginas **no están configuradas con permisos por política** (como el resto que usa `@attribute [Authorize(Policy = "...")]`) y por lo tanto no son controlables desde la matriz de **Usuarios y Roles**.

## Hallazgos

### 1) Páginas sin `Authorize` por política

| Página | Ruta | Situación |
|---|---|---|
| `Components/Pages/About.razor` | `/about` | No tiene `Authorize` ni `Policy` |
| `Components/Pages/Login.razor` | `/` | Pública (esperado para login) |

### 2) Página autenticada pero sin política específica

| Página | Ruta | Situación |
|---|---|---|
| `Components/Pages/Home.razor` | `/home` | Tiene `@attribute [Authorize]` (sin `Policy`) |

## Interpretación

- `About` no está alineada con las páginas que usan permisos por política y queda accesible sin control por rol/permiso.
- `Home` requiere sesión iniciada, pero **no** depende de un permiso específico configurable en Usuarios/Roles.
- `Login` normalmente debe permanecer pública.

## Estado de políticas

Las políticas usadas por las páginas protegidas (`documentos.ver`, `documentos.editar`, `vinculacion.gestionar`, `reclusos.gestionar`, `renovaciones.gestionar`, `configuracion.catalogos`, `configuracion.rangos`, `seguridad.usuarios_roles`) sí existen en `Services/Security/AppPermissions.cs`.

## Recomendación

1. Definir si `About` debe ser pública o protegida por permiso.
2. Definir si `Home` debe seguir con acceso por autenticación simple o migrar a una política específica para que también sea gestionable desde Usuarios/Roles.
