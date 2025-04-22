using System;
using System.Collections.Generic; // Necesario para List
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace ProyectoFinal_G8.Controllers
{
    [Authorize]
    public class MascotasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<MascotasController> _logger;

        private bool IsAdminOrVet => User.IsInRole("Admin") || User.IsInRole("Veterinario");

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            string? currentUserIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out userId))
            {
                _logger.LogError("MascotasController: No se pudo obtener ID usuario.");
                return false;
            }
            return true;
        }

        public MascotasController(ProyectoFinal_G8Context context, UserManager<Usuario> userManager, ILogger<MascotasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Mascotas
        public async Task<IActionResult> Index(string? searchString)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt))
            {
                TempData["ErrorMessage"] = "Error al identificar al usuario.";
                return RedirectToAction("Index", "Home");
            }

            string? effectiveSearchString = IsAdminOrVet ? searchString : null;
            if (IsAdminOrVet && !string.IsNullOrEmpty(effectiveSearchString))
            {
                ViewData["CurrentFilter"] = effectiveSearchString;
            }
            else
            {
                ViewData["CurrentFilter"] = "";
            }

            IQueryable<Mascota> mascotasQuery = _context.Mascotas.Include(m => m.Dueño);

            // ***** CORRECCIÓN AQUÍ: Inicializar vistaTitulo *****
            string vistaTitulo = "Mascotas"; // Valor inicial por defecto

            if (IsAdminOrVet && !string.IsNullOrEmpty(effectiveSearchString))
            {
                _logger.LogInformation($"Admin/Vet buscando mascotas: '{effectiveSearchString}'");
                mascotasQuery = mascotasQuery.Where(m => m.Nombre.Contains(effectiveSearchString) ||
                                                       (m.Dueño != null && m.Dueño.Nombre != null && m.Dueño.Nombre.Contains(effectiveSearchString)));
                vistaTitulo = $"Mascotas (Resultados para '{effectiveSearchString}')";
            }

            if (IsAdminOrVet)
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Admin/Vet) viendo mascotas.");
                if (string.IsNullOrEmpty(effectiveSearchString)) // Solo asignar título general si NO hubo búsqueda
                {
                    vistaTitulo = "Gestión de Mascotas";
                }
            }
            else // Es Cliente
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Cliente ID: {userIdAsInt}) viendo sus mascotas.");
                mascotasQuery = mascotasQuery.Where(m => m.IdUsuarioDueño == userIdAsInt);
                vistaTitulo = "Mis Mascotas";
            }

            ViewData["VistaTitulo"] = vistaTitulo; // Ahora 'vistaTitulo' siempre tendrá un valor
            var mascotas = await mascotasQuery.OrderBy(m => m.Nombre).ToListAsync();
            return View(mascotas);
        }

        // GET: Mascotas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas
                .Include(m => m.Dueño)
                // .Include(m => m.Citas)              // Descomentar si se necesita
                // .Include(m => m.HistorialesMedicos) // Descomentar si se necesita
                .FirstOrDefaultAsync(m => m.IdMascota == id);

            if (mascota == null) { return NotFound(); }

            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó ver detalles de mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para ver detalles de esta mascota.";
                return RedirectToAction(nameof(Index));
            }

            return View(mascota);
        }

        // GET: Mascotas/Create
        public async Task<IActionResult> Create()
        {
            if (IsAdminOrVet)
            {
                await LoadDueñosAsync();
            }
            return View();
        }

        // POST: Mascotas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Especie,Raza,FechaNacimiento,IdUsuarioDueño")] Mascota mascota)
        {
            ModelState.Remove(nameof(Mascota.Dueño));
            ModelState.Remove(nameof(Mascota.Citas));
            ModelState.Remove(nameof(Mascota.HistorialesMedicos));

            if (User.IsInRole("Cliente"))
            {
                if (!TryGetCurrentUserId(out int userIdAsInt))
                {
                    ModelState.AddModelError(string.Empty, "No se pudo identificar al usuario.");
                    return View(mascota);
                }
                mascota.IdUsuarioDueño = userIdAsInt;
                ModelState.Remove(nameof(Mascota.IdUsuarioDueño));
            }

            if (mascota.FechaNacimiento.HasValue && mascota.FechaNacimiento.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(Mascota.FechaNacimiento), "La fecha de nacimiento no puede ser futura.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(mascota);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Mascota registrada exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al crear mascota.");
                    ModelState.AddModelError("", "Ocurrió un error inesperado al guardar la mascota.");
                }
            }

            if (IsAdminOrVet)
            {
                await LoadDueñosAsync(mascota.IdUsuarioDueño);
            }
            return View(mascota);
        }

        // GET: Mascotas/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas.FindAsync(id);
            if (mascota == null) { return NotFound(); }

            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó GET edit mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para editar esta mascota.";
                return RedirectToAction(nameof(Index));
            }

            if (IsAdminOrVet)
            {
                await LoadDueñosAsync(mascota.IdUsuarioDueño);
            }

            return View(mascota);
        }

        // POST: Mascotas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdMascota,Nombre,Especie,Raza,FechaNacimiento,IdUsuarioDueño")] Mascota mascota)
        {
            if (id != mascota.IdMascota) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            if (User.IsInRole("Cliente"))
            {
                var mascotaOriginal = await _context.Mascotas.AsNoTracking().FirstOrDefaultAsync(m => m.IdMascota == id);
                if (mascotaOriginal == null || mascotaOriginal.IdUsuarioDueño != userIdAsInt)
                {
                    _logger.LogWarning($"Cliente {userIdAsInt} intentó POST edit mascota ajena {id}.");
                    TempData["ErrorMessage"] = "No tienes permiso para guardar cambios en esta mascota.";
                    return RedirectToAction(nameof(Index));
                }
                mascota.IdUsuarioDueño = userIdAsInt;
            }

            ModelState.Remove(nameof(Mascota.Dueño));
            ModelState.Remove(nameof(Mascota.Citas));
            ModelState.Remove(nameof(Mascota.HistorialesMedicos));
            if (User.IsInRole("Cliente"))
            {
                ModelState.Remove(nameof(Mascota.IdUsuarioDueño));
            }

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
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Error Concurrencia Edit Mascota Id {MascotaId}", id);
                    if (!await MascotaExists(mascota.IdMascota)) { return NotFound(); }
                    else { ModelState.AddModelError("", "La mascota fue modificada por otro usuario. Intente de nuevo."); }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error Edit Mascota Id {MascotaId}", id);
                    ModelState.AddModelError("", "Ocurrió un error inesperado al guardar los cambios.");
                }

                if (!ModelState.IsValid && ModelState.Values.SelectMany(v => v.Errors).Any(e => e.ErrorMessage.Contains("modificada por otro usuario") || e.ErrorMessage.Contains("inesperado")))
                {
                    TempData["ErrorMessage"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Error al guardar.";
                    return RedirectToAction(nameof(Edit), new { id = id });
                }
                else if (!ModelState.IsValid)
                {
                    // Error de validación normal, recargar datos y mostrar vista
                }
                else
                {
                    return RedirectToAction(nameof(Index)); // Redirigir si se guardó OK
                }
            }

            if (IsAdminOrVet)
            {
                await LoadDueñosAsync(mascota.IdUsuarioDueño);
            }
            return View(mascota);
        }

        // GET: Mascotas/Delete/5
        public async Task<IActionResult> Delete(int? id, bool? saveChangesError = false)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas
                .Include(m => m.Dueño)
                .FirstOrDefaultAsync(m => m.IdMascota == id);

            if (mascota == null) { return NotFound(); }

            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó GET delete mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para eliminar esta mascota.";
                return RedirectToAction(nameof(Index));
            }

            if (saveChangesError.GetValueOrDefault())
            {
                ViewData["ErrorMessage"] = TempData["ErrorMessage"] ?? "Error al intentar eliminar la mascota. Verifique que no tenga datos asociados.";
                TempData.Remove("ErrorMessage"); // Limpiar para no mostrarlo de nuevo si recarga
            }

            return View(mascota);
        }

        // POST: Mascotas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID.");

            var mascota = await _context.Mascotas.FindAsync(id);

            if (mascota == null)
            {
                TempData["ErrorMessage"] = "Mascota no encontrada.";
                return RedirectToAction(nameof(Index));
            }

            if (User.IsInRole("Cliente") && mascota.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {userIdAsInt} intentó POST delete mascota ajena {id}.");
                TempData["ErrorMessage"] = "No tienes permiso para eliminar esta mascota.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _context.Mascotas.Remove(mascota);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Mascota eliminada exitosamente.";
                _logger.LogInformation($"Mascota ID {id} eliminada por usuario ID {userIdAsInt}.");
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, $"Error BD eliminando Mascota ID {id}. InnerEx: {dbEx.InnerException?.Message}");
                TempData["ErrorMessage"] = "No se pudo eliminar la mascota. Asegúrate de que no tenga citas o historiales asociados.";
                return RedirectToAction(nameof(Delete), new { id = id, saveChangesError = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error general eliminando Mascota ID {id}.");
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar la mascota.";
                return RedirectToAction(nameof(Delete), new { id = id, saveChangesError = true });
            }

            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para cargar Dueños (Clientes)
        private async Task LoadDueñosAsync(object? selectedDueño = null)
        {
            var dueños = await _userManager.GetUsersInRoleAsync("Cliente");
            // Usar Nombre o UserName para el texto del dropdown
            ViewData["IdUsuarioDueño"] = new SelectList(dueños.OrderBy(u => u.Nombre ?? u.UserName), "Id", "Nombre", selectedDueño);
            _logger.LogDebug($"LoadDueñosAsync: Cargados {dueños.Count} posibles dueños (clientes).");
        }


        private async Task<bool> MascotaExists(int id)
        {
            return await _context.Mascotas.AnyAsync(e => e.IdMascota == id);
        }
    }
}