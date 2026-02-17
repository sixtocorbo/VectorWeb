using Microsoft.AspNetCore.Authorization;
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
// CONFIGURACIÓN DE BASE DE DATOS
// NOTA: Se eliminó "EnableRetryOnFailure" para permitir transacciones manuales.
// -----------------------------------------------------------------------------

// 1. DbContextFactory (Para Blazor Server)
builder.Services.AddDbContextFactory<SecretariaDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. DbContext Normal (Transient)
builder.Services.AddDbContext<SecretariaDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")),
    contextLifetime: ServiceLifetime.Transient);

// -----------------------------------------------------------------------------

// Repositorio Genérico (Transient para evitar bloqueos por concurrencia)
builder.Services.AddTransient(typeof(IRepository<>), typeof(Repository<>));

// Servicios de Negocio (Scoped)
builder.Services.AddScoped<NumeracionRangoService>();
builder.Services.AddScoped<DocumentoVinculacionService>();
builder.Services.AddScoped<RenovacionesService>();
builder.Services.AddScoped<RolePermissionService>();
builder.Services.AddScoped<PermissionAuditService>();

// Caché
builder.Services.AddMemoryCache();

// Seguridad y Permisos
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorizationCore(options =>
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
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();