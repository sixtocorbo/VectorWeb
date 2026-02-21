using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using VectorWeb.Auth;
using VectorWeb.Components;
using VectorWeb.Models;
using VectorWeb.Repositories;
using VectorWeb.Services;
using VectorWeb.Services.Security;
using VectorWeb.Services.Security.Authorization;
using VectorWeb.Services.Security.Audit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// -----------------------------------------------------------------------------
// CONFIGURACIÓN DE BASE DE DATOS PARA BLAZOR SERVER
// -----------------------------------------------------------------------------

// Registro de la factoría de contextos (Recomendado para Blazor Server)
// Esto permite que el repositorio cree y destruya contextos por cada operación,
// evitando el error 'Invalid attempt to call ReadAsync when reader is closed'.
builder.Services.AddDbContextFactory<SecretariaDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// -----------------------------------------------------------------------------

// Repositorio Genérico (Transient)
// Ahora se resolverá usando IDbContextFactory inyectado en su constructor.
builder.Services.AddTransient(typeof(IRepository<>), typeof(Repository<>));

// Servicios de Negocio (Scoped)
builder.Services.AddScoped<DocumentoPlazosService>();
builder.Services.AddScoped<NumeracionRangoService>();
builder.Services.AddScoped<DocumentoVinculacionService>();
builder.Services.AddScoped<RenovacionesService>();
builder.Services.AddScoped<RolePermissionService>();
builder.Services.AddScoped<PermissionAuditService>();
builder.Services.AddScoped<DatabaseBackupService>();

// Caché
builder.Services.AddMemoryCache();

// Seguridad y Permisos
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permiso in AppPermissions.Todos)
    {
        options.AddPolicy(permiso, policy => policy.Requirements.Add(new PermissionRequirement(permiso)));
    }
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<ProtectedSessionStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
