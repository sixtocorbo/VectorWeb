using VectorWeb.Components;
using Microsoft.EntityFrameworkCore; // <--- NUEVO: Para poder usar EF Core
using VectorWeb.Models;              // <--- NUEVO: Para encontrar tu SecretariaDbContext

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// --- ZONA DE CONFIGURACIÓN DE BASE DE DATOS ---
// Aquí le decimos a la app que use SQL Server con la cadena que pusiste en appsettings.json
builder.Services.AddDbContext<SecretariaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
// ---------------------------------------------

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();