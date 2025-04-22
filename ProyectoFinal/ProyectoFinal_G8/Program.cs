using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using ProyectoFinal_G8.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;


var builder = WebApplication.CreateBuilder(args);

// --- Add services to the container ---

// 1. Configuración del DbContext
var connectionString = builder.Configuration.GetConnectionString("PF_G8"); // Revisa que sea el nombre correcto en appsettings.Development.json
builder.Services.AddDbContext<ProyectoFinal_G8Context>(options =>
    options.UseSqlServer(connectionString));

// 2. Configuración de ASP.NET Core Identity
builder.Services.AddIdentity<Usuario, Rol>(options => {
    // Configuración de políticas de contraseña, bloqueo, etc.
    options.Password.RequireDigit = false; // Ejemplo: No requerir dígitos
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true; // Requerir email único
})
    .AddEntityFrameworkStores<ProyectoFinal_G8Context>() // Vincula Identity con tu DbContext
    .AddDefaultTokenProviders(); // Para funciones como reseteo de contraseña

// Registrar el servicio IEmailSender (usando la implementación ficticia)
builder.Services.AddTransient<IEmailSender, DummyEmailSender>();

// 3. Añadir soporte para Razor Pages (necesario para Identity UI)
builder.Services.AddRazorPages();

// 4. Configuración de MVC
builder.Services.AddControllersWithViews();


// --- Configure the HTTP request pipeline ---
var app = builder.Build();

// --- SEED DATA: Crear Roles, Usuarios Esenciales y Tipos de Cita ---
// Se ejecuta después de construir la app, pero antes de que empiece a escuchar peticiones
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Iniciando seeding de roles, usuarios y tipos de cita...");
        var userManager = services.GetRequiredService<UserManager<Usuario>>(); // Obtener UserManager
        var roleManager = services.GetRequiredService<RoleManager<Rol>>();     // Obtener RoleManager
        var dbContext = services.GetRequiredService<ProyectoFinal_G8Context>(); // Obtener DbContext

        // 1. Crear Roles primero
        await SeedRolesAsync(roleManager, logger);

        // 2. Crear Usuarios después
        await SeedUsersAsync(userManager, roleManager, logger);

        // 3. Crear Tipos de Cita
        await SeedTiposCitaAsync(dbContext, logger);

        logger.LogInformation("Seeding completado.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ocurrió un error durante el seeding de la base de datos.");
    }
}

// Función asíncrona para crear los roles si no existen
async Task SeedRolesAsync(RoleManager<Rol> roleManager, ILogger<Program> logger)
{
    string[] roleNames = { "Admin", "Veterinario", "Cliente" }; // Roles a crear
    logger.LogInformation($"Verificando/creando roles: {string.Join(", ", roleNames)}");
    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            // Crear el rol si no existe
            var result = await roleManager.CreateAsync(new Rol { Name = roleName });
            if (result.Succeeded)
            {
                logger.LogInformation($"Rol '{roleName}' creado exitosamente.");
            }
            else
            {
                logger.LogError($"Error creando rol '{roleName}'. Errores: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}

// Función asíncrona para crear usuarios esenciales si no existen
async Task SeedUsersAsync(UserManager<Usuario> userManager, RoleManager<Rol> roleManager, ILogger<Program> logger)
{
    // --- Usuario Admin ---
    string adminEmail = "admin@mail.com";
    string adminNombre = "Admin Principal";
    string adminPassword = "Admin.123"; 
    string adminRoleName = "Admin";

    logger.LogInformation($"Verificando usuario Admin: {adminEmail}");
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var adminRole = await roleManager.FindByNameAsync(adminRoleName);
        if (adminRole == null) { logger.LogError($"Rol '{adminRoleName}' no existe. No se puede crear usuario Admin."); return; }

        Usuario adminUser = new Usuario
        {
            UserName = adminEmail,
            Email = adminEmail,
            Nombre = adminNombre,
            IdRol = adminRole.Id,
            EmailConfirmed = true
        };
        IdentityResult result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            logger.LogInformation($"Usuario '{adminUser.UserName}' creado.");
            await userManager.AddToRoleAsync(adminUser, adminRoleName);
            logger.LogInformation($"Usuario '{adminUser.UserName}' asignado al rol '{adminRoleName}'.");
        }
        else { logger.LogError($"Error creando usuario '{adminUser.UserName}'. Errores: {string.Join(", ", result.Errors.Select(e => e.Description))}"); }
    }

    // --- Usuario Veterinario (Dani) ---
    string vetEmail = "dani@mail.com";
    string vetNombre = "Dani Veterinario";
    string vetPassword = "Admin.123"; 
    string vetRoleName = "Veterinario";

    logger.LogInformation($"Verificando usuario Veterinario: {vetEmail}");
    if (await userManager.FindByEmailAsync(vetEmail) == null)
    {
        var vetRole = await roleManager.FindByNameAsync(vetRoleName);
        if (vetRole == null) { logger.LogError($"Rol '{vetRoleName}' no existe. No se puede crear usuario Veterinario."); return; }

        Usuario vetUser = new Usuario
        {
            UserName = vetEmail,
            Email = vetEmail,
            Nombre = vetNombre,
            IdRol = vetRole.Id,
            EmailConfirmed = true
        };
        IdentityResult result = await userManager.CreateAsync(vetUser, vetPassword);
        if (result.Succeeded)
        {
            logger.LogInformation($"Usuario '{vetUser.UserName}' creado.");
            await userManager.AddToRoleAsync(vetUser, vetRoleName);
            logger.LogInformation($"Usuario '{vetUser.UserName}' asignado al rol '{vetRoleName}'.");
        }
        else { logger.LogError($"Error creando usuario '{vetUser.UserName}'. Errores: {string.Join(", ", result.Errors.Select(e => e.Description))}"); }
    }
}

// Función SeedTiposCitaAsync
async Task SeedTiposCitaAsync(ProyectoFinal_G8Context context, ILogger<Program> logger)
{
    logger.LogInformation("Verificando/creando Tipos de Cita...");
    if (!await context.TiposCita.AnyAsync())
    {
        logger.LogInformation("Creando Tipos de Cita iniciales...");
        var tipos = new List<TipoCita>
        {
            new TipoCita { Nombre = "Consulta Médica", DuracionMinutos = 60 },
            new TipoCita { Nombre = "Vacunación Cachorro", DuracionMinutos = 60 },
            new TipoCita { Nombre = "Vacunación Adulto", DuracionMinutos = 30 },
            new TipoCita { Nombre = "Desparasitación", DuracionMinutos = 30 },
            new TipoCita { Nombre = "Revisión Post-Operatoria", DuracionMinutos = 45 },
            new TipoCita { Nombre = "Emergencia", DuracionMinutos = 90 },
            new TipoCita { Nombre = "Cirugía Menor", DuracionMinutos = 120 }
        };
        await context.TiposCita.AddRangeAsync(tipos);
        await context.SaveChangesAsync();
        logger.LogInformation("Tipos de Cita iniciales creados.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Middleware de autenticación y autorización
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages(); // Mapea las páginas de Identity (/Identity/Account/Login, etc.)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"); // Mapea tus controladores MVC

app.Run(); // Debe ser la última línea ejecutable