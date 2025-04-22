using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // <-- Añadir using para ILogger
using ProyectoFinal_G8.Models;
using System.Security.Claims;

namespace ProyectoFinal_G8.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<CitasController> _logger; // <-- Añadir campo para Logger

        // Modificar constructor para inyectar ILogger
        public CitasController(
            ProyectoFinal_G8Context context,
            UserManager<Usuario> userManager,
            ILogger<CitasController> logger) // <-- Añadir ILogger al constructor
        {
            _context = context;
            _userManager = userManager;
            _logger = logger; // <-- Asignar Logger
        }

        // GET: Citas o MisCitas
        public async Task<IActionResult> Index()
        {
            string currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserId) || !int.TryParse(currentUserId, out int userIdAsInt))
            {
                _logger.LogError("Index: No se pudo obtener o parsear el ID del usuario.");
                return Unauthorized("No se pudo obtener o parsear el ID del usuario.");
            }

            IQueryable<Cita> citasQuery = _context.Citas
                                                 .Include(c => c.Mascota)
                                                    .ThenInclude(m => m.Dueño)
                                                 .Include(c => c.Veterinario);

            IList<Cita> citas;

            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Admin/Vet) obteniendo todas las citas.");
                citas = await citasQuery.OrderByDescending(c => c.FechaHora).ToListAsync();
                ViewData["VistaTitulo"] = "Gestión de Citas";
            }
            else if (User.IsInRole("Cliente"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Cliente) obteniendo sus citas (ID: {userIdAsInt}).");
                citas = await citasQuery
                               .Where(c => c.Mascota != null && c.Mascota.IdUsuarioDueño == userIdAsInt)
                               .OrderByDescending(c => c.FechaHora)
                               .ToListAsync();
                ViewData["VistaTitulo"] = "Mis Citas";
            }
            else
            {
                _logger.LogWarning($"Usuario {User.Identity?.Name} con rol desconocido intentó acceder a citas.");
                citas = new List<Cita>();
                ViewData["VistaTitulo"] = "Citas";
            }

            return View(citas);
        }

        // GET: Citas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { _logger.LogWarning("Details: ID es null."); return NotFound(); }

            string currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserId) || !int.TryParse(currentUserId, out int userIdAsInt))
            {
                _logger.LogError($"Details({id}): No se pudo obtener o parsear el ID del usuario.");
                return Unauthorized("No se pudo obtener o parsear el ID del usuario.");
            }

            _logger.LogInformation($"Buscando detalles para cita ID: {id}");
            var cita = await _context.Citas
                .Include(c => c.Mascota).ThenInclude(m => m.Dueño)
                .Include(c => c.Veterinario)
                .FirstOrDefaultAsync(m => m.IdCita == id);

            if (cita == null) { _logger.LogWarning($"Details: Cita con ID {id} no encontrada."); return NotFound(); }

            // Verificación de Permiso
            if (User.IsInRole("Cliente") && cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) intentó ver detalles de cita ajena (ID: {id}, Dueño Mascota ID: {cita.Mascota?.IdUsuarioDueño}).");
                return Forbid();
            }

            return View(cita);
        }

        // GET: Citas/Create
        public async Task<IActionResult> Create()
        {
            string currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserId) || !int.TryParse(currentUserId, out int userIdAsInt))
            {
                _logger.LogError("Create GET: No se pudo obtener o parsear el ID del usuario.");
                return Unauthorized("No se pudo obtener o parsear el ID del usuario.");
            }

            await LoadVeterinariosAsync();

            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Admin/Vet) cargando vista Create (todas las mascotas).");
                ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre");
            }
            else if (User.IsInRole("Cliente"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Cliente ID: {userIdAsInt}) cargando vista Create (sus mascotas).");
                await LoadMisMascotasAsync(userIdAsInt);
                if (!((SelectList)ViewData["IdMascota"]).Any())
                {
                    _logger.LogWarning($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) no tiene mascotas registradas. Redirigiendo.");
                    TempData["ErrorMessage"] = "Debe registrar una mascota antes de poder solicitar una cita.";
                    return RedirectToAction("Index", "Mascotas");
                }
            }
            else
            {
                _logger.LogWarning($"Usuario {User.Identity?.Name} con rol desconocido intentó acceder a Create GET.");
                return Forbid();
            }

            var nuevaCita = new Cita { FechaHora = DateTime.Now.Date.AddHours(9) };
            return View(nuevaCita);
        }

        // POST: Citas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,Motivo,Estado,Notas")] Cita cita)
        {
            string currentUserId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserId) || !int.TryParse(currentUserId, out int userIdAsInt))
            {
                _logger.LogError("Create POST: No se pudo obtener o parsear el ID del usuario.");
                return Unauthorized("No se pudo obtener o parsear el ID del usuario.");
            }

            if (User.IsInRole("Cliente"))
            {
                var mascotaSeleccionada = await _context.Mascotas
                                                    .AsNoTracking()
                                                    .FirstOrDefaultAsync(m => m.IdMascota == cita.IdMascota);

                if (mascotaSeleccionada == null || mascotaSeleccionada.IdUsuarioDueño != userIdAsInt)
                {
                    _logger.LogWarning($"Cliente {User.Identity?.Name} intentó crear cita con mascota inválida/ajena (ID Mascota: {cita.IdMascota}).");
                    ModelState.AddModelError("IdMascota", "Mascota inválida o no pertenece al usuario.");
                    await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
                    await LoadMisMascotasAsync(userIdAsInt, cita.IdMascota);
                    return View(cita);
                }
                cita.Estado ??= "Pendiente";
            }
            else if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                cita.Estado ??= "Confirmada";
            }
            else
            {
                _logger.LogError($"Usuario {User.Identity?.Name} con rol no autorizado intentó ejecutar Create POST.");
                return Forbid();
            }

            ModelState.Remove("Mascota");
            ModelState.Remove("Veterinario");

            if (ModelState.IsValid)
            {
                _logger.LogInformation($"Intentando guardar nueva cita para Mascota ID: {cita.IdMascota} por Usuario: {User.Identity?.Name}");
                _context.Add(cita);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Cita ID: {cita.IdCita} guardada exitosamente.");
                    TempData["SuccessMessage"] = User.IsInRole("Cliente") ? "Solicitud de cita enviada." : "Cita creada.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error guardando nueva cita para Mascota ID: {cita.IdMascota}");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar la cita.");
                }
            }
            else
            {
                _logger.LogWarning($"Create POST: ModelState inválido para Usuario: {User.Identity?.Name}");
            }

            // Si falla validación o guardado, recargar listas según rol
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre", cita.IdMascota);
            }
            else
            {
                await LoadMisMascotasAsync(userIdAsInt, cita.IdMascota);
            }
            return View(cita);
        }


        // GET: Citas/Edit/5
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            _logger.LogInformation($"Cargando vista Edit para Cita ID: {id}");
            var cita = await _context.Citas.FindAsync(id);
            if (cita == null) { _logger.LogWarning($"Edit GET: Cita ID {id} no encontrada."); return NotFound(); }
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre", cita.IdMascota);
            return View(cita);
        }

        // POST: Citas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,Motivo,Estado,Notas")] Cita cita)
        {
            if (id != cita.IdCita) { _logger.LogWarning($"Edit POST: ID de ruta ({id}) no coincide con ID de modelo ({cita.IdCita})."); return NotFound(); }

            ModelState.Remove("Mascota");
            ModelState.Remove("Veterinario");

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation($"Intentando actualizar Cita ID: {id}");
                    _context.Update(cita);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Cita ID: {id} actualizada.");
                    TempData["SuccessMessage"] = "Cita actualizada.";
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, $"Error de concurrencia actualizando Cita ID: {id}");
                    if (!await CitaExists(cita.IdCita)) { _logger.LogWarning($"Edit POST: Cita ID {id} ya no existe."); return NotFound(); }
                    else { throw; } // Relanzar si no es porque no existe
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error actualizando Cita ID: {id}");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar la cita.");
                    // Recargar listas y devolver vista si hay error general
                    await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
                    ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre", cita.IdMascota);
                    return View(cita);
                }
                return RedirectToAction(nameof(Index));
            }
            // Si ModelState no es válido
            _logger.LogWarning($"Edit POST: ModelState inválido para Cita ID: {id}");
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre", cita.IdMascota);
            return View(cita);
        }

        // GET: Citas/Delete/5
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            _logger.LogInformation($"Cargando vista Delete para Cita ID: {id}");
            var cita = await _context.Citas
                .Include(c => c.Mascota).Include(c => c.Veterinario)
                .FirstOrDefaultAsync(m => m.IdCita == id);
            if (cita == null) { _logger.LogWarning($"Delete GET: Cita ID {id} no encontrada."); return NotFound(); }
            return View(cita);
        }

        // POST: Citas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogWarning($"Intentando eliminar Cita ID: {id}");
            var cita = await _context.Citas.FindAsync(id);
            if (cita != null)
            {
                try
                {
                    _context.Citas.Remove(cita);
                    await _context.SaveChangesAsync();
                    _logger.LogWarning($"Cita ID: {id} eliminada por {User.Identity?.Name}.");
                    TempData["SuccessMessage"] = "Cita eliminada.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error eliminando Cita ID: {id}");
                    TempData["ErrorMessage"] = "Ocurrió un error al eliminar la cita.";
                }
            }
            else { _logger.LogWarning($"Delete POST: Cita ID {id} no encontrada."); TempData["ErrorMessage"] = "Cita no encontrada."; }
            return RedirectToAction(nameof(Index));
        }

        // --- Métodos Auxiliares ---
        private async Task LoadVeterinariosAsync(object? selectedVeterinario = null)
        {
            var veterinarios = await _userManager.GetUsersInRoleAsync("Veterinario");
            ViewData["IdUsuarioVeterinario"] = new SelectList(veterinarios.OrderBy(u => u.Nombre), "Id", "Nombre", selectedVeterinario);
        }

        private async Task LoadMisMascotasAsync(int currentUserId, object? selectedMascota = null)
        {
            var misMascotas = await _context.Mascotas
                                      .Where(m => m.IdUsuarioDueño == currentUserId)
                                      .OrderBy(m => m.Nombre)
                                      .ToListAsync();
            ViewData["IdMascota"] = new SelectList(misMascotas, "IdMascota", "Nombre", selectedMascota);
            _logger.LogDebug($"Cargadas {misMascotas.Count} mascotas para el usuario ID: {currentUserId}");
        }

        private async Task<bool> CitaExists(int id)
        {
            return await _context.Citas.AnyAsync(e => e.IdCita == id);
        }
    }
}