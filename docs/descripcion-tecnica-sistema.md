# Descripción técnica integral del sistema VectorWeb

## 1) Visión general de la arquitectura

VectorWeb es una aplicación **Blazor Server** con renderizado interactivo en servidor. La solución está organizada por capas ligeras:

1. **Presentación (UI)**: componentes Razor en `Components/Pages` y layout/navegación en `Components/Layout`.
2. **Aplicación/negocio**: servicios en `Services/` que encapsulan reglas operativas (plazos, rangos, vinculación, renovaciones, seguridad, respaldo).
3. **Acceso a datos**:
   - `SecretariaDbContext` como mapeo EF Core de la base SQL Server.
   - Repositorio genérico `IRepository<T>/Repository<T>` para operaciones CRUD comunes.
4. **Seguridad**: autenticación por `AuthenticationStateProvider` personalizado y autorización por políticas de permisos.

El arranque de la aplicación registra `DbContextFactory`, repositorio, servicios de negocio, caché, autorización por permisos y estado de autenticación en sesión protegida.

## 2) Flujo de arranque y composición (Dependency Injection)

En `Program.cs` se configura:

- **Razor Components** con modo interactivo de servidor.
- **`AddDbContextFactory<SecretariaDbContext>`** (en lugar de `AddDbContext`) para que cada operación cree/descarta su contexto, patrón especialmente útil en Blazor Server para evitar contextos de larga vida.
- **Repositorio genérico transient** (`IRepository<>` → `Repository<>`).
- **Servicios scoped** de dominio: plazos, rangos, vinculación, renovaciones, seguridad y respaldo.
- **Autorización por políticas dinámicas**: se crea una policy por cada permiso de `AppPermissions.Todos`.
- **`CustomAuthStateProvider`** y `ProtectedSessionStorage` para persistencia de sesión del usuario entre recargas del cliente.

Resultado: cada circuito de Blazor obtiene servicios con estado controlado y con fronteras claras entre UI, reglas de negocio y persistencia.

## 3) Modelo de datos y entidades clave

`SecretariaDbContext` mapea gran parte del dominio operativo:

- **Documental**: `MaeDocumentos`, `TraMovimientos`, `TraAdjuntoDocumentos`.
- **Catálogos**: `CatTipoDocumento`, `CatOficina`, `CatEstado`, `CatUsuario`.
- **Configuración**: `CfgTiemposRespuesta`, `CfgSistemaParametros`.
- **Numeración**: `MaeNumeracionRangos`, `MaeNumeracionBitacoras`, `MaeCuposSecretaria`.
- **Seguridad/Auditoría**: `SegAuditoriaPermisos`, `EventosSistema`.
- **Renovaciones (Art.120)**: `TraSalidasLaborales` + `TraSalidasLaboralesDocumentoRespaldos`.

Además de `DbSet<>`, el `OnModelCreating` define claves, índices, defaults, relaciones FK y restricciones de negocio persistidas en SQL (p.ej. unicidad de usuario login, índices por fechas y relaciones oficina/usuario).

## 4) Patrón de acceso a datos

### 4.1 Repositorio genérico

`Repository<T>` usa `IDbContextFactory<SecretariaDbContext>` y crea un contexto por método:

- Lecturas (`GetAllAsync`, `FindAsync`, `GetFilteredAsync`) con `AsNoTracking`.
- Escrituras (`AddAsync`, `UpdateAsync`, `DeleteAsync`) con `SaveChangesAsync`.
- Soporte de includes por cadena en `GetFilteredAsync`.

Esto reduce acoplamiento entre servicios y EF Core directo, aunque varios servicios de dominio también usan `DbContext` para consultas complejas/transacciones.

### 4.2 Uso directo de DbContext en servicios

Servicios como `DocumentoVinculacionService`, `RenovacionesService` y `NumeracionRangoService` usan directamente el contexto cuando necesitan:

- Transacciones explícitas.
- Operaciones batch.
- Consultas con `Include`, agrupaciones o SQL crudo.

## 5) Seguridad: autenticación, autorización y auditoría

### 5.1 Autenticación

`CustomAuthStateProvider`:

- Persiste en sesión un `AuthSessionUser`.
- Construye un `ClaimsPrincipal` con claims de identidad (`NameIdentifier`, `Name`, `Role`, `IdOficina`).
- Carga permisos del rol y los agrega como claims de tipo `permission`.
- Permite refrescar permisos (`RefreshCurrentUserPermissionsAsync`) y cerrar sesión.

### 5.2 Matriz de permisos por rol

`AppPermissions` define:

- Constantes de permisos (documentos, vinculación, reclusos, renovaciones, configuración y seguridad).
- Etiquetas y agrupaciones para UI.
- Mapeo permiso → páginas/rutas.

`RolePermissionService`:

- Normaliza roles.
- Obtiene matriz desde parámetro `SEGURIDAD_PERMISOS_POR_ROL` (`CfgSistemaParametros`) en formato JSON.
- Si falla/ausente, aplica matriz por defecto (`ADMINISTRADOR` con todo, `OPERADOR` con permisos base).
- Cachea resultado en memoria por rol para mejorar rendimiento.

### 5.3 Autorización y auditoría

`PermissionAuthorizationHandler` evalúa si el rol del usuario contiene el permiso requerido por policy.

- Si concede: `context.Succeed`.
- Si deniega: registra warning y envía evento a `PermissionAuditService`.

`PermissionAuditService` persiste denegaciones en `SegAuditoriaPermisos` con usuario, rol, permiso, módulo y ruta.

## 6) Procesos funcionales principales

### 6.1 Gestión documental (bandeja y ciclo)

La página de documentos consume entidades y relaciones (`IdEstadoActualNavigation`, `IdTipoNavigation`, padre/hijo) para presentar:

- Estado semaforizado,
- Datos de identificación,
- Relación de vinculación,
- Acciones (editar, pase, historial, recorrido).

En términos de proceso, el documento evoluciona por estados, movimientos y asignación de oficina actual.

### 6.2 Cálculo de plazos y vencimientos

`DocumentoPlazosService` consulta `CfgTiemposRespuesta` por tipo/prioridad y calcula vencimiento:

- Menor a 30 días: suma **días hábiles** (`FechaHelper`).
- 30 días o más: suma días corridos.

Esto determina semáforo/criticidad de atención en UI y reportes.

### 6.3 Numeración y cupos anuales

`NumeracionRangoService` concentra la lógica de folios/números oficiales:

- Consulta de rangos, bitácora y libro mayor de cupos.
- Consumo de siguiente número con SQL `UPDATE ... OUTPUT` para incrementar de forma atómica.
- Alta/edición/eliminación de rangos con registro de bitácora.
- Configuración de cupos por tipo/año y cálculo de sugerencias de nuevos rangos (inicio/fin sugeridos según saldo).

Es el núcleo para evitar colisiones y sobreconsumo de numeración.

### 6.4 Vinculación de documentos (padre-hijo)

`DocumentoVinculacionService.VincularAsync`:

1. Normaliza IDs solicitados.
2. Carga esqueleto relacional.
3. Valida existencia del padre.
4. Previene ciclos (el padre no puede quedar descendiente de su hijo).
5. Calcula subárbol por hijo.
6. Actualiza `IdDocumentoPadre` y propaga `IdHiloConversacion` a descendientes.
7. Confirma en transacción.

El resultado retorna detalle por hijo (éxito/error, reasignación, afectación de descendientes).

### 6.5 Renovaciones (Art. 120)

`RenovacionesService` maneja salidas laborales:

- Lista renovaciones activas/inactivas con cálculo de días restantes y estado (`Ok`, `Alerta`, `Vencida`, `Inactiva`).
- Guarda/actualiza la salida y sus documentos de respaldo en transacción.
- Cambia estado activo/inactivo y agrega motivo de cese en observaciones serializadas JSON.
- Resuelve parámetro configurable de alerta (`RENOVACIONES_DIAS_ALERTA`) con valor por defecto.

### 6.6 Respaldo de base de datos

`DatabaseBackupService.CreateBackupAsync`:

- Obtiene nombre de DB desde conexión activa.
- Define carpeta de backup por configuración (`BackupPath`) o ruta por defecto.
- Ejecuta `BACKUP DATABASE ... TO DISK` vía SQL crudo.
- Retorna metadata del respaldo.

## 7) Interconexiones entre clases (mapa práctico)

- `Program.cs` instancia todo el grafo DI.
- `Routes.razor` + `AuthorizeRouteView` aplican autenticación/autorización a páginas.
- `NavMenu.razor` usa `AuthorizeView` por policy para mostrar solo opciones permitidas.
- `Login.razor` (flujo de autenticación) invoca `CustomAuthStateProvider`.
- `CustomAuthStateProvider` consulta `RolePermissionService` para claims de permisos.
- `PermissionAuthorizationHandler` también consulta `RolePermissionService` para decidir acceso.
- Cuando niega acceso, llama `PermissionAuditService`, que persiste en DB.
- Páginas de negocio consumen servicios de dominio (`RenovacionesService`, `DocumentoVinculacionService`, etc.) y éstos consumen `DbContextFactory` y/o `Repository<T>`.

## 8) Decisiones técnicas relevantes

1. **DbContextFactory en Blazor Server** para evitar problemas de concurrencia/vida útil del contexto.
2. **Políticas por permiso** en lugar de solo rol, permitiendo granularidad fina.
3. **Matriz de permisos serializada en DB** para administración dinámica sin recompilar.
4. **Auditoría de denegaciones** para trazabilidad de seguridad.
5. **Transacciones explícitas** en procesos sensibles (vinculación y renovaciones).
6. **SQL atómico para numeración** minimizando carreras al consumir consecutivos.

## 9) Riesgos y consideraciones operativas

- `RevalidateCacheAsync` en `RolePermissionService` hoy no limpia caché realmente; si cambian permisos en caliente, puede haber retraso hasta expiración.
- El respaldo depende de permisos del motor SQL y acceso de escritura a carpeta destino.
- La estrategia de repositorio convive con DbContext directo; conviene mantener criterios claros para no duplicar reglas.

## 10) Resumen ejecutivo del proceso extremo a extremo

1. Usuario inicia sesión → se crea principal con claims + permisos.
2. Navegación y rutas filtran vistas según policies.
3. Páginas ejecutan casos de uso (documentos, vinculación, renovaciones, configuración).
4. Servicios aplican validaciones de negocio y persisten usando EF Core/transacciones.
5. Seguridad audita denegaciones y configuración parametrizable afecta comportamiento (plazos, alertas, permisos).

Con este diseño, VectorWeb ofrece un flujo documental con control de acceso fino, trazabilidad y operación parametrizable sobre SQL Server.
