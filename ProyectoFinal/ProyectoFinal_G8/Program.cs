using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using ProyectoFinal_G8.Services;
// --- Añadir estos usings para Seeding y Logging ---
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // Necesario para ILogger en el bloque de seeding
// -------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// --- Add services to the container ---

// 1. Configuración del DbContext
var connectionString = builder.Configuration.GetConnectionString("PF_G8");
builder.Services.AddDbContext<ProyectoFinal_G8Context>(options =>
    options.UseSqlServer(connectionString));

// 2. Configuración de ASP.NET Core Identity (SOLO UNA LLAMADA)
builder.Services.AddIdentity<Usuario, Rol>(options => {
    // Configura aquí las políticas de contraseña, bloqueo, etc. si lo deseas
    options.Password.RequireDigit = false; // Ejemplo: No requerir dígitos
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true; // Requerir email único
    // options.SignIn.RequireConfirmedAccount = false; // Opcional: Requerir confirmación de email
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

// --- SEED DATA: Crear Roles Esenciales ---
// Se ejecuta después de construir la app, pero antes de que empiece a escuchar peticiones
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>(); // Obtener logger
    try
    {
        logger.LogInformation("Iniciando seeding de roles...");
        var roleManager = services.GetRequiredService<RoleManager<Rol>>();
        await SeedRolesAsync(roleManager, logger); // Pasar logger a la función
        logger.LogInformation("Seeding de roles completado.");
        // Opcional: Podrías llamar aquí a una función para crear un usuario Admin inicial
        // var userManager = services.GetRequiredService<UserManager<Usuario>>();
        // await SeedAdminUserAsync(userManager, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ocurrió un error durante el seeding de la base de datos.");
        // Dependiendo de la criticidad, podrías querer detener la aplicación aquí
        // throw;
    }
}

// Función asíncrona para crear los roles si no existen
async Task SeedRolesAsync(RoleManager<Rol> roleManager, ILogger<Program> logger)
{
    string[] roleNames = { "Admin", "Veterinario", "Cliente" }; // Roles a crear
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
                // Loggear errores si la creación del rol falla
                logger.LogError($"Error creando rol '{roleName}'. Errores: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        // else { logger.LogInformation($"Rol '{roleName}' ya existe."); } // Log opcional
    }
}

// Función opcional para crear usuario Admin (ejemplo básico)
/*
async Task SeedAdminUserAsync(UserManager<Usuario> userManager, ILogger<Program> logger)
{
    string adminEmail = "admin@miclinica.com"; // Email del admin
    string adminPassword = "PasswordSuperSeguro123!"; // ¡Usa una contraseña segura y configúrala correctamente!
    string adminNombre = "Administrador Principal"; // Nombre para el usuario admin

    // Verificar si el usuario admin ya existe
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        Usuario adminUser = new Usuario
        {
            UserName = adminEmail,
            Email = adminEmail,
            Nombre = adminNombre, // Asignar el nombre
            EmailConfirmed = true // Confirmarlo directamente
            // IdRol se podría asignar aquí si buscas el ID del rol "Admin",
            // pero es mejor confiar en la asignación de roles de Identity
        };

        // Crear el usuario admin
        IdentityResult result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            logger.LogInformation($"Usuario Admin '{adminEmail}' creado exitosamente.");
            // Asignar el rol "Admin" al nuevo usuario
            // Asegúrate que el rol "Admin" fue creado por SeedRolesAsync
            var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
             if (roleResult.Succeeded)
             {
                 logger.LogInformation($"Rol 'Admin' asignado a '{adminEmail}'.");
             }
             else
             {
                  logger.LogError($"Error asignando rol 'Admin' a '{adminEmail}'. Errores: {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
             }
        }
        else
        {
            logger.LogError($"Error creando usuario Admin '{adminEmail}'. Errores: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    // else { logger.LogInformation($"Usuario Admin '{adminEmail}' ya existe."); } // Log opcional
}
*/
// --- FIN SEED DATA ---


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run(); // app.Run() debe ser la última línea ejecutable