using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProyectoFinal_G8.Models; 

namespace ProyectoFinal_G8.Controllers
{
    [Authorize]
    public class HistorialMedicosController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly ILogger<HistorialMedicosController> _logger;
        private readonly UserManager<Usuario> _userManager; 

        public HistorialMedicosController(
            ProyectoFinal_G8Context context,
            ILogger<HistorialMedicosController> logger,
            UserManager<Usuario> userManager) 
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        [Authorize(Roles = "Admin,Veterinario,Cliente")]
        public async Task<IActionResult> Index(int? mascotaId)
        {
            _logger.LogInformation($"Accediendo al Index de HistorialMedico. Filtro Mascota ID: {mascotaId}");

            // Obtener ID usuario actual (como string primero)
            string currentUserIdString = _userManager.GetUserId(User);
            // Convertir a int (asumiendo que tu IdUsuario es int, ajusta si es necesario)
            int.TryParse(currentUserIdString, out int currentUserId);

            bool isCliente = User.IsInRole("Cliente");

            if (isCliente)
            {
                if (!mascotaId.HasValue)
                {
                    _logger.LogWarning($"Cliente {currentUserIdString} intentó acceder a historial sin especificar mascotaId.");
                    TempData["ErrorMessage"] = "Debe seleccionar una mascota para ver su historial.";
                    ViewData["TituloHistorial"] = "Seleccione una Mascota";
                    return View(new List<HistorialMedico>());
                }

                // Verificar que la mascota pertenece al cliente actual usando IdUsuarioDueño
                // *** ¡IMPORTANTE! Usando IdUsuarioDueño de Mascota ***
                bool mascotaPerteneceAlCliente = await _context.Mascotas
                    .AnyAsync(m => m.IdMascota == mascotaId.Value && m.IdUsuarioDueño == currentUserId); // CORREGIDO aquí

                if (!mascotaPerteneceAlCliente)
                {
                    _logger.LogWarning($"Cliente {currentUserIdString} intentó acceder al historial de mascotaId {mascotaId.Value} que no le pertenece.");
                    TempData["ErrorMessage"] = "No tiene permiso para ver el historial de esta mascota.";
                    ViewData["TituloHistorial"] = "Acceso Denegado";
                    return View(new List<HistorialMedico>());
                }
                _logger.LogInformation($"Cliente {currentUserIdString} autorizado para ver historial de mascotaId {mascotaId.Value}.");
            }

            // --- Lógica de consulta ---
            var historialesQuery = _context.HistorialMedicos
                                           .Include(h => h.Mascota)
                                               .ThenInclude(m => m.Dueño) // Asume que Dueño es la prop de navegación a Usuario
                                           .AsQueryable();

            if (mascotaId.HasValue)
            {
                var mascota = await _context.Mascotas
                                            .Select(m => new { m.IdMascota, m.Nombre })
                                            .FirstOrDefaultAsync(m => m.IdMascota == mascotaId.Value);

                if (mascota != null)
                {
                    // El filtro Where se aplica para todos los roles si mascotaId tiene valor
                    historialesQuery = historialesQuery.Where(h => h.IdMascota == mascotaId.Value);
                    ViewData["TituloHistorial"] = $"Historial Médico de {mascota.Nombre}";
                    ViewData["MascotaIdFiltrada"] = mascotaId.Value;
                }
                else
                {
                    _logger.LogWarning($"Index GET: No se encontró mascota con ID {mascotaId.Value}.");
                    TempData["ErrorMessage"] = $"No se encontró la mascota con ID {mascotaId.Value}.";
                    ViewData["TituloHistorial"] = "Mascota no encontrada";
                    historialesQuery = historialesQuery.Where(h => false);
                }
            }
            else
            {
                if (isCliente) { return View(new List<HistorialMedico>()); } // Cliente siempre necesita mascotaId
                ViewData["TituloHistorial"] = "Historial Médico General";
            }

            // Pasar el ID del usuario actual a la vista si se necesita (como en tu ejemplo de Citas)
            ViewData["CurrentUserID"] = currentUserId;

            var historiales = await historialesQuery
                                    .OrderByDescending(h => h.FechaRegistro)
                                    .ToListAsync();

            return View(historiales);
        }

        [Authorize(Roles = "Admin,Veterinario,Cliente")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { return NotFound(); }

            var historialMedico = await _context.HistorialMedicos
                .Include(h => h.Mascota)
                    .ThenInclude(m => m.Dueño) // Incluye el Usuario dueño a través de Mascota
                .FirstOrDefaultAsync(m => m.IdHistorial == id);

            if (historialMedico == null) { return NotFound(); }

            if (User.IsInRole("Cliente"))
            {
                string currentUserIdString = _userManager.GetUserId(User);
                int.TryParse(currentUserIdString, out int currentUserId);
                // *** ¡IMPORTANTE! Usando IdUsuarioDueño de Mascota ***
                if (historialMedico.Mascota?.IdUsuarioDueño != currentUserId) // CORREGIDO aquí
                {
                    _logger.LogWarning($"Cliente {currentUserIdString} intentó ver Details del historial {id} que no le pertenece.");
                    return Forbid();
                }
                _logger.LogInformation($"Cliente {currentUserIdString} autorizado para ver Details del historial {id}.");
            }

            ViewData["MascotaIdOriginal"] = historialMedico.IdMascota;
            // Pasar ID de usuario si fuera necesario en la vista Details
            string currentUserIdStr = _userManager.GetUserId(User);
            int.TryParse(currentUserIdStr, out int currentUsrId);
            ViewData["CurrentUserID"] = currentUsrId;

            return View(historialMedico);
        }

        // --- Métodos Create, Edit, Delete (sin cambios en la lógica interna, solo verificar Authorize) ---

        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> Create(int? mascotaId)
        {
            var historial = new HistorialMedico { FechaRegistro = DateTime.Now };
            bool isMascotaPreselected = false;
            if (mascotaId.HasValue)
            {
                bool mascotaExiste = await _context.Mascotas.AnyAsync(m => m.IdMascota == mascotaId.Value);
                if (mascotaExiste) { historial.IdMascota = mascotaId.Value; isMascotaPreselected = true; }
                else { TempData["ErrorMessage"] = $"Mascota ID {mascotaId.Value} no encontrada."; }
            }
            ViewData["IdMascota"] = await GetMascotasSelectListAsync(historial.IdMascota);
            ViewData["IsMascotaPreselected"] = isMascotaPreselected;
            return View(historial);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> Create([Bind("IdMascota,FechaRegistro,Descripcion,Tratamiento,Notas")] HistorialMedico historialMedico)
        {
            ModelState.Remove(nameof(historialMedico.Mascota));
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(historialMedico);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Registro creado exitosamente.";
                    return RedirectToAction(nameof(Index), new { mascotaId = historialMedico.IdMascota }); // Redirigir a la lista filtrada
                }
                catch (Exception ex) { /*...*/ }
            }
            ViewData["IdMascota"] = await GetMascotasSelectListAsync(historialMedico.IdMascota);
            return View(historialMedico);
        }

        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) { return NotFound(); }
            var historialMedico = await _context.HistorialMedicos.FindAsync(id);
            if (historialMedico == null) { return NotFound(); }
            ViewData["IdMascota"] = await GetMascotasSelectListAsync(historialMedico.IdMascota);
            return View(historialMedico);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> Edit(int id, [Bind("IdHistorial,IdMascota,FechaRegistro,Descripcion,Tratamiento,Notas")] HistorialMedico historialMedico)
        {
            if (id != historialMedico.IdHistorial) { return NotFound(); }
            ModelState.Remove(nameof(historialMedico.Mascota));
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(historialMedico);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Registro actualizado exitosamente.";
                    return RedirectToAction(nameof(Index), new { mascotaId = historialMedico.IdMascota });
                }
                catch (DbUpdateConcurrencyException ex) { /*...*/ }
                catch (Exception ex) { /*...*/ }
            }
            ViewData["IdMascota"] = await GetMascotasSelectListAsync(historialMedico.IdMascota);
            return View(historialMedico);
        }

        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) { return NotFound(); }
            var historialMedico = await _context.HistorialMedicos
                .Include(h => h.Mascota) // Incluir para obtener IdMascota para la redirección
                .FirstOrDefaultAsync(m => m.IdHistorial == id);
            if (historialMedico == null) { return NotFound(); }
            return View(historialMedico);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var historialMedico = await _context.HistorialMedicos.FindAsync(id);
            int? mascotaIdRedir = historialMedico?.IdMascota;

            if (historialMedico != null)
            {
                try { _context.HistorialMedicos.Remove(historialMedico); await _context.SaveChangesAsync(); TempData["SuccessMessage"] = "Registro eliminado."; }
                catch (Exception ex) { TempData["ErrorMessage"] = "Error al eliminar."; return RedirectToAction(nameof(Delete), new { id = id }); }
            }
            else { TempData["ErrorMessage"] = "Registro no encontrado."; }

            return RedirectToAction(nameof(Index), new { mascotaId = mascotaIdRedir });
        }

        // --- Métodos privados existentes (sin cambios) ---
        private bool HistorialMedicoExists(int id) { /*...*/ return _context.HistorialMedicos.Any(e => e.IdHistorial == id); }
        private async Task<SelectList> GetMascotasSelectListAsync(object? selectedValue = null)
        { /*...*/
            // Este método ya incluye el Dueño.Nombre, está bien.
            var mascotasData = await _context.Mascotas
                                          .Include(m => m.Dueño)
                                          .OrderBy(m => m.Nombre)
                                          .Select(m => new {
                                              Id = m.IdMascota,
                                              DisplayText = $"{m.Nombre} (Dueño: {m.Dueño.Nombre ?? "N/A"})"
                                          })
                                          .ToListAsync();
            string? currentSelection = selectedValue?.ToString();
            return new SelectList(mascotasData, "Id", "DisplayText", currentSelection);
        }
    }
}