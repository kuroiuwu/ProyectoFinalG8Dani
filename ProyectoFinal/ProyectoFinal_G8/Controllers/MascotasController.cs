using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging; // Añadido para Logging

namespace ProyectoFinal_G8.Controllers
{
    [Authorize] // Todo el controlador requiere login
    public class MascotasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<MascotasController> _logger; // Añadir logger

        // Helper para obtener ID de usuario (igual que en CitasController)
        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            string? currentUserIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out userId))
            {
                _logger.LogError("MascotasController: No se pudo obtener o parsear el ID del usuario logueado.");
                return false;
            }
            return true;
        }

        public MascotasController(ProyectoFinal_G8Context context, UserManager<Usuario> userManager, ILogger<MascotasController> logger) // Añadir ILogger
        {
            _context = context;
            _userManager = userManager;
            _logger = logger; // Inyectar logger
        }


        // GET: Mascotas (Modificado para filtrar por rol)
        public async Task<IActionResult> Index()
        {
            if (!TryGetCurrentUserId(out int userIdAsInt))
            {
                return Unauthorized("No se pudo obtener el ID del usuario.");
            }

            IQueryable<Mascota> mascotasQuery = _context.Mascotas.Include(m => m.Dueño);
            string vistaTitulo;

            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Admin/Vet) viendo todas las mascotas.");
                // No se aplica filtro adicional, ven todas
                vistaTitulo = "Gestión de Mascotas";
            }
            else if (User.IsInRole("Cliente"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Cliente ID: {userIdAsInt}) viendo sus mascotas.");
                // Filtrar por dueño
                mascotasQuery = mascotasQuery.Where(m => m.IdUsuarioDueño == userIdAsInt);
                vistaTitulo = "Mis Mascotas";
            }
            else
            {
                _logger.LogWarning($"Usuario {User.Identity?.Name} con rol desconocido intentó acceder a Mascotas/Index.");
                // Devuelve vista vacía o Forbid/Unauthorized
                return View(new List<Mascota>()); // Lista vacía
                                                  // return Forbid();
            }

            ViewData["VistaTitulo"] = vistaTitulo;
            var mascotas = await mascotasQuery.OrderBy(m => m.Nombre).ToListAsync();
            return View(mascotas);
        }

        // GET: Mascotas/Details/5 (Modificado para verificar permiso de Cliente)
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas
                .Include(m => m.Dueño)
                .Include(m => m.Citas)
                .Include(m => m.HistorialesMedicos)
                .FirstOrDefaultAsync(m => m.IdMascota == id);

            if (mascota == null) { return NotFound(); }

            // --- Verificación de Permiso para Cliente ---
            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó ver detalles de mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para ver detalles de esta mascota.";
                return RedirectToAction(nameof(Index));
            }
            // --- Fin Verificación ---

            return View(mascota);
        }

        // GET: Mascotas/Create (Modificado para rol Cliente)
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                // Admin/Vet pueden asignar dueño, cargar lista de posibles dueños (solo Clientes)
                await LoadDueñosAsync();
            }
            else if (User.IsInRole("Cliente"))
            {
                // Cliente crea mascota para sí mismo, no necesita seleccionar dueño
                ViewData["IdUsuarioDueño"] = null; // No poblar dropdown para cliente
            }
            else
            {
                return Forbid(); // Otros roles no pueden crear
            }

            return View();
        }

        // POST: Mascotas/Create (Modificado para rol Cliente)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Especie,Raza,FechaNacimiento,IdUsuarioDueño")] Mascota mascota) // Quitar IdMascota del Bind
        {
            ModelState.Remove(nameof(Mascota.Dueño));
            ModelState.Remove(nameof(Mascota.Citas));
            ModelState.Remove(nameof(Mascota.HistorialesMedicos));

            // --- Asignación de Dueño para Cliente ---
            if (User.IsInRole("Cliente"))
            {
                if (!TryGetCurrentUserId(out int userIdAsInt))
                {
                    ModelState.AddModelError(string.Empty, "No se pudo identificar al usuario.");
                    // No recargamos dropdown de dueño porque no se muestra
                    return View(mascota);
                }
                // Forzar el dueño a ser el cliente actual
                mascota.IdUsuarioDueño = userIdAsInt;
                // Quitar posible error de validación del campo IdUsuarioDueño si el dropdown no estaba presente
                ModelState.Remove(nameof(Mascota.IdUsuarioDueño));
            }
            // --- Fin Asignación Dueño ---

            // Validar fecha nacimiento no futura (opcional)
            if (mascota.FechaNacimiento.HasValue && mascota.FechaNacimiento.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(Mascota.FechaNacimiento), "La fecha de nacimiento no puede ser futura.");
            }


            if (ModelState.IsValid)
            {
                _context.Add(mascota);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Mascota registrada exitosamente.";
                return RedirectToAction(nameof(Index));
            }

            // Si falla y es Admin/Vet, recargar lista de dueños
            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                await LoadDueñosAsync(mascota.IdUsuarioDueño);
            }
            return View(mascota);
        }

        // GET: Mascotas/Edit/5 (Modificado para verificar permiso de Cliente)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas.FindAsync(id);
            if (mascota == null) { return NotFound(); }

            // --- Verificación de Permiso para Cliente ---
            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó editar mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para editar esta mascota.";
                return RedirectToAction(nameof(Index));
            }
            // --- Fin Verificación ---

            // Cargar lista de dueños SOLO si es Admin/Vet (cliente no puede cambiar dueño)
            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                await LoadDueñosAsync(mascota.IdUsuarioDueño);
            }

            return View(mascota);
        }

        // POST: Mascotas/Edit/5 (Modificado para verificar permiso de Cliente)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdMascota,Nombre,Especie,Raza,FechaNacimiento,IdUsuarioDueño")] Mascota mascota)
        {
            if (id != mascota.IdMascota) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            // --- Verificación de Permiso para Cliente ANTES de guardar ---
            if (User.IsInRole("Cliente"))
            {
                // Cargar la mascota original para asegurar que pertenece al usuario
                var mascotaOriginal = await _context.Mascotas
                                               .AsNoTracking() // Para no interferir con el Update
                                               .FirstOrDefaultAsync(m => m.IdMascota == id);

                if (mascotaOriginal == null || mascotaOriginal.IdUsuarioDueño != userIdAsInt)
                {
                    _logger.LogWarning($"Cliente {userIdAsInt} intentó guardar cambios en mascota ajena {id}.");
                    TempData["ErrorMessage"] = "No tienes permiso para guardar cambios en esta mascota.";
                    return RedirectToAction(nameof(Index));
                }
                // Si es cliente, asegurarse que IdUsuarioDueño no cambie
                mascota.IdUsuarioDueño = userIdAsInt;
            }
            // --- Fin Verificación ---


            ModelState.Remove(nameof(Mascota.Dueño));
            ModelState.Remove(nameof(Mascota.Citas));
            ModelState.Remove(nameof(Mascota.HistorialesMedicos));
            // Si es cliente, IdUsuarioDueño no es editable, removemos posible error
            if (User.IsInRole("Cliente"))
            {
                ModelState.Remove(nameof(Mascota.IdUsuarioDueño));
            }

            // Validar fecha nacimiento no futura (opcional)
            if (mascota.FechaNacimiento.HasValue && mascota.FechaNacimiento.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(Mascota.FechaNacimiento), "La fecha de nacimiento no puede ser futura.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(mascota);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Mascota actualizada exitosamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await MascotaExists(mascota.IdMascota)) { return NotFound(); }
                    else { throw; }
                }
                return RedirectToAction(nameof(Index));
            }

            // Si falla y es Admin/Vet, recargar lista de dueños
            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                await LoadDueñosAsync(mascota.IdUsuarioDueño);
            }
            return View(mascota);
        }

        // GET: Mascotas/Delete/5 (Modificado para verificar permiso de Cliente)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas
                .Include(m => m.Dueño)
                .FirstOrDefaultAsync(m => m.IdMascota == id);

            if (mascota == null) { return NotFound(); }

            // --- Verificación de Permiso para Cliente ---
            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó ver confirmación de borrado de mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para eliminar esta mascota.";
                return RedirectToAction(nameof(Index));
            }
            // --- Fin Verificación ---

            return View(mascota);
        }

        // POST: Mascotas/Delete/5 (Modificado para verificar permiso de Cliente)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas.FindAsync(id); // Buscar mascota

            if (mascota == null)
            {
                TempData["ErrorMessage"] = "Mascota no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            // --- Verificación de Permiso para Cliente ANTES de borrar ---
            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó confirmar borrado de mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para eliminar esta mascota.";
                return RedirectToAction(nameof(Index));
            }
            // --- Fin Verificación ---

            try
            {
                _context.Mascotas.Remove(mascota);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Mascota eliminada exitosamente.";
                _logger.LogWarning($"Mascota ID {id} eliminada por usuario ID {userIdAsInt}.");
            }
            catch (DbUpdateException dbEx)
            {
                // Manejar error si hay FKs que impiden borrar (ej: citas con Restrict)
                _logger.LogError(dbEx, $"Error BD eliminando Mascota ID {id}. InnerEx: {dbEx.InnerException?.Message}");
                TempData["ErrorMessage"] = "No se pudo eliminar la mascota. Asegúrate de que no tenga citas o historiales asociados.";
                // Devolver a la vista de confirmación para mostrar el error
                return RedirectToAction(nameof(Delete), new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error general eliminando Mascota ID {id}.");
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar la mascota.";
                return RedirectToAction(nameof(Delete), new { id = id });
            }

            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para cargar Dueños (Solo Clientes para el dropdown de Admin/Vet)
        private async Task LoadDueñosAsync(object? selectedDueño = null)
        {
            // Cargar solo usuarios con el rol "Cliente"
            var dueños = await _userManager.GetUsersInRoleAsync("Cliente");
            ViewData["IdUsuarioDueño"] = new SelectList(dueños.OrderBy(u => u.Nombre), "Id", "Nombre", selectedDueño);
            _logger.LogDebug($"LoadDueñosAsync: Cargados {dueños.Count} posibles dueños (clientes).");
        }


        private async Task<bool> MascotaExists(int id)
        {
            return await _context.Mascotas.AnyAsync(e => e.IdMascota == id);
        }
    }
}