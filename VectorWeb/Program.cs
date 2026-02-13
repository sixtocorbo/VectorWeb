using VectorWeb.Components;
using Microsoft.EntityFrameworkCore;
using VectorWeb.Models;
using VectorWeb.Repositories; // <--- ¡IMPORTANTE! AGREGA ESTO

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 1. Configuración de Base de Datos (Ya la tenías)
builder.Services.AddDbContext<SecretariaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. REGISTRO DEL REPOSITORIO (¡ESTO ES LO QUE FALTA!) <---
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

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