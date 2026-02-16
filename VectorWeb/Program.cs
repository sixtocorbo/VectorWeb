using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using VectorWeb.Auth;
using VectorWeb.Components;
using VectorWeb.Models;
using VectorWeb.Repositories;
using VectorWeb.Services;
using VectorWeb.Services.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
builder.Services.AddDbContextFactory<SecretariaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<SecretariaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")),
    contextLifetime: ServiceLifetime.Transient);

// Generic repository
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<NumeracionRangoService>();
builder.Services.AddScoped<DocumentoVinculacionService>();
builder.Services.AddScoped<RenovacionesService>();
builder.Services.AddScoped<RolePermissionService>();

// Blazor auth services
builder.Services.AddAuthorizationCore(options =>
{
    foreach (var permiso in AppPermissions.Todos)
    {
        options.AddPolicy(permiso, policy => policy.RequireClaim(AppPermissions.ClaimType, permiso));
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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
