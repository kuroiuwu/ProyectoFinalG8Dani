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

        // *** Añadido Helper para verificar roles ***
        private bool IsAdminOrVet => User.IsInRole("Admin") || User.IsInRole("Veterinario");

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
        public async Task<IActionResult> Index(int? mascotaId, string? searchString) // Permitir searchString nulo
        {
            // Procesar searchString *solo* si es Admin o Vet
            string? effectiveSearchString = IsAdminOrVet ? searchString : null;

            _logger.LogInformation($"Accediendo HistorialMedico Index. Rol Admin/Vet: {IsAdminOrVet}. Mascota ID: {mascotaId}, Search: '{effectiveSearchString}'");

            string currentUserIdString = _userManager.GetUserId(User);
            int.TryParse(currentUserIdString, out int currentUserId);
            bool isCliente = User.IsInRole("Cliente"); // Podemos usar !IsAdminOrVet si solo hay esos 3 roles

            // Guardar el filtro de búsqueda *solo* si es Admin/Vet y hay algo
            if (IsAdminOrVet && !string.IsNullOrEmpty(effectiveSearchString))
            {
                ViewData["CurrentFilter"] = effectiveSearchString;
            }
            else
            {
                ViewData["CurrentFilter"] = ""; // Asegurarse que esté vacío si no aplica
            }


            // --- Lógica de Cliente ---
            if (isCliente)
            {
                if (!mascotaId.HasValue)
                {
                    TempData["InfoMessage"] = "Seleccione una de sus mascotas para ver su historial."; // Mensaje más informativo
                    ViewData["TituloHistorial"] = "Seleccione Mascota";
                    await LoadMascotasClienteForViewAsync(currentUserId); // Cargar dropdown para cliente
                    return View(new List<HistorialMedico>()); // Mostrar vista vacía con dropdown
                }

                bool mascotaPerteneceAlCliente = await _context.Mascotas
                    .AnyAsync(m => m.IdMascota == mascotaId.Value && m.IdUsuarioDueño == currentUserId);

                if (!mascotaPerteneceAlCliente)
                {
                    TempData["ErrorMessage"] = "No tiene permiso para ver el historial de esta mascota.";
                    ViewData["TituloHistorial"] = "Acceso Denegado";
                    await LoadMascotasClienteForViewAsync(currentUserId); // Cargar dropdown
                    return View(new List<HistorialMedico>());
                }
                _logger.LogInformation($"Cliente {currentUserIdString} autorizado para ver historial mascotaId {mascotaId.Value}.");
            }
            // --- Lógica de Admin/Vet ---
            else // Si es Admin o Vet
            {
                // Cargar dropdown de *todas* las mascotas para el filtro si no hay mascotaId específica
                if (!mascotaId.HasValue)
                {
                    ViewData["MascotasSearchList"] = await GetMascotasSelectListAsync();
                }
            }

            // --- Query Base ---
            var historialesQuery = _context.HistorialMedicos
                                            .Include(h => h.Mascota)
                                                .ThenInclude(m => m.Dueño)
                                            .AsQueryable();

            // --- Aplicar Filtros ---

            // 1. Filtro por Mascota Específica (para cualquier rol autorizado)
            if (mascotaId.HasValue)
            {
                var mascota = await _context.Mascotas
                                            .Select(m => new { m.IdMascota, m.Nombre })
                                            .FirstOrDefaultAsync(m => m.IdMascota == mascotaId.Value);

                if (mascota != null)
                {
                    historialesQuery = historialesQuery.Where(h => h.IdMascota == mascotaId.Value);
                    ViewData["TituloHistorial"] = $"Historial Médico de {mascota.Nombre}";
                    ViewData["MascotaIdFiltrada"] = mascotaId.Value;

                    // Aplicar búsqueda de texto DENTRO del historial de la mascota, *solo si es Admin/Vet*
                    if (IsAdminOrVet && !string.IsNullOrEmpty(effectiveSearchString))
                    {
                        historialesQuery = historialesQuery.Where(h => h.Descripcion.Contains(effectiveSearchString) ||
                                                                      (h.Tratamiento != null && h.Tratamiento.Contains(effectiveSearchString)) ||
                                                                      (h.Notas != null && h.Notas.Contains(effectiveSearchString)));
                        ViewData["TituloHistorial"] += $" (filtrado por '{effectiveSearchString}')";
                    }
                }
                else
                {
                    // Mascota no encontrada (puede pasar si se manipula la URL)
                    TempData["ErrorMessage"] = $"No se encontró la mascota con ID {mascotaId.Value}.";
                    ViewData["TituloHistorial"] = "Mascota no encontrada";
                    historialesQuery = historialesQuery.Where(h => false); // No devolver resultados
                    if (isCliente) await LoadMascotasClienteForViewAsync(currentUserId); // Recargar dropdown cliente
                    else await GetMascotasSelectListAsync(); // Recargar dropdown admin/vet
                }
            }
            // 2. Filtro por SearchString (Solo para Admin/Vet y si NO se filtra por mascotaId)
            else if (IsAdminOrVet && !string.IsNullOrEmpty(effectiveSearchString))
            {
                historialesQuery = historialesQuery.Where(h => h.Mascota.Nombre.Contains(effectiveSearchString) ||
                                                               (h.Mascota.Dueño != null && h.Mascota.Dueño.Nombre.Contains(effectiveSearchString))); // Asume que Dueños tienen Nombre
                ViewData["TituloHistorial"] = $"Historial Médico (Resultados para '{effectiveSearchString}')";
            }
            // 3. Vista General (Solo Admin/Vet sin filtros)
            else if (IsAdminOrVet)
            {
                ViewData["TituloHistorial"] = "Historial Médico General";
            }
            // 4. Caso Cliente sin mascotaId (ya manejado al inicio, pero por seguridad)
            else if (isCliente)
            {
                ViewData["TituloHistorial"] = "Seleccione Mascota";
                historialesQuery = historialesQuery.Where(h => false); // Asegurar que no muestre nada
                await LoadMascotasClienteForViewAsync(currentUserId); // Cargar dropdown
            }

            ViewData["CurrentUserID"] = currentUserId; // Pasar ID actual a la vista

            var historiales = await historialesQuery
                                    .OrderByDescending(h => h.FechaRegistro)
                                    .ToListAsync();

            return View(historiales);
        }

        // --- Método Details (sin cambios) ---
        [Authorize(Roles = "Admin,Veterinario,Cliente")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { return NotFound(); }
            var historialMedico = await _context.HistorialMedicos
                .Include(h => h.Mascota)
                    .ThenInclude(m => m.Dueño)
                .FirstOrDefaultAsync(m => m.IdHistorial == id);
            if (historialMedico == null) { return NotFound(); }
            if (User.IsInRole("Cliente"))
            {
                string currentUserIdString = _userManager.GetUserId(User);
                int.TryParse(currentUserIdString, out int currentUserId);
                if (historialMedico.Mascota?.IdUsuarioDueño != currentUserId) { return Forbid(); }
            }
            ViewData["MascotaIdOriginal"] = historialMedico.IdMascota;
            string currentUserIdStr = _userManager.GetUserId(User);
            int.TryParse(currentUserIdStr, out int currentUsrId);
            ViewData["CurrentUserID"] = currentUsrId;
            return View(historialMedico);
        }

        // --- Métodos Create (sin cambios) ---
        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> Create(int? mascotaId)
        {
            var historial = new HistorialMedico { FechaRegistro = DateTime.Now };
            bool isMascotaPreselected = false;
            string? mascotaNombre = null;
            if (mascotaId.HasValue)
            {
                var mascota = await _context.Mascotas.FindAsync(mascotaId.Value);
                if (mascota != null) { historial.IdMascota = mascotaId.Value; isMascotaPreselected = true; mascotaNombre = mascota.Nombre; }
                else { TempData["ErrorMessage"] = $"Mascota ID {mascotaId.Value} no encontrada."; }
            }
            ViewData["IdMascota"] = await GetMascotasSelectListAsync(historial.IdMascota);
            ViewData["IsMascotaPreselected"] = isMascotaPreselected;
            ViewData["MascotaNombre"] = mascotaNombre;
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
                    return RedirectToAction(nameof(Index), new { mascotaId = historialMedico.IdMascota });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al crear registro historial.");
                    ModelState.AddModelError("", "Ocurrió un error al guardar.");
                }
            }
            ViewData["IdMascota"] = await GetMascotasSelectListAsync(historialMedico.IdMascota);
            ViewData["IsMascotaPreselected"] = await _context.Mascotas.AnyAsync(m => m.IdMascota == historialMedico.IdMascota); // Recheck
            ViewData["MascotaNombre"] = await _context.Mascotas.Where(m => m.IdMascota == historialMedico.IdMascota).Select(m => m.Nombre).FirstOrDefaultAsync(); // Get name again
            return View(historialMedico);
        }

        // --- Métodos Edit (sin cambios) ---
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
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency Error Edit Historial ID {HistorialId}", id);
                    if (!HistorialMedicoExists(historialMedico.IdHistorial)) { return NotFound(); }
                    else { ModelState.AddModelError("", "El registro fue modificado. Intente de nuevo."); }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error Edit Historial ID {HistorialId}", id);
                    ModelState.AddModelError("", "Ocurrió un error al guardar.");
                }
            }
            ViewData["IdMascota"] = await GetMascotasSelectListAsync(historialMedico.IdMascota);
            return View(historialMedico);
        }

        // --- Métodos Delete (sin cambios) ---
        [Authorize(Roles = "Admin,Veterinario")]
        public async Task<IActionResult> Delete(int? id, bool? saveChangesError = false)
        {
            if (id == null) { return NotFound(); }
            var historialMedico = await _context.HistorialMedicos
                .Include(h => h.Mascota)
                    .ThenInclude(m => m.Dueño)
                .FirstOrDefaultAsync(m => m.IdHistorial == id);
            if (historialMedico == null) { return NotFound(); }
            if (saveChangesError.GetValueOrDefault()) { ViewData["ErrorMessage"] = "Error al eliminar. Inténtelo de nuevo."; }
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
                try
                {
                    _context.HistorialMedicos.Remove(historialMedico);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Registro eliminado exitosamente.";
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "DB Error Deleting Historial ID {HistorialId}", id);
                    TempData["ErrorMessage"] = "No se pudo eliminar. Puede tener datos asociados.";
                    return RedirectToAction(nameof(Delete), new { id = id, saveChangesError = true });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error Deleting Historial ID {HistorialId}", id);
                    TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar.";
                    return RedirectToAction(nameof(Delete), new { id = id, saveChangesError = true });
                }
            }
            else { TempData["ErrorMessage"] = "Registro no encontrado."; }
            return RedirectToAction(nameof(Index), new { mascotaId = mascotaIdRedir });
        }

        // --- Métodos privados (sin cambios) ---
        private bool HistorialMedicoExists(int id)
        {
            return _context.HistorialMedicos.Any(e => e.IdHistorial == id);
        }

        private async Task<SelectList> GetMascotasSelectListAsync(object? selectedValue = null)
        {
            var mascotasData = await _context.Mascotas
                                        .Include(m => m.Dueño)
                                        .OrderBy(m => m.Nombre)
                                        .Select(m => new {
                                            Id = m.IdMascota,
                                            DisplayText = $"{m.Nombre} (Dueño: {m.Dueño.Nombre ?? m.Dueño.UserName ?? "N/A"})"
                                        })
                                        .ToListAsync();
            string? currentSelection = selectedValue?.ToString();
            return new SelectList(mascotasData, "Id", "DisplayText", currentSelection);
        }

        private async Task LoadMascotasClienteForViewAsync(int clienteId, int? selectedMascotaId = null)
        {
            var misMascotas = await _context.Mascotas
               .Where(m => m.IdUsuarioDueño == clienteId)
               .OrderBy(m => m.Nombre)
               .Select(m => new { m.IdMascota, m.Nombre })
               .ToListAsync();
            ViewData["MisMascotasList"] = new SelectList(misMascotas, "IdMascota", "Nombre", selectedMascotaId);
        }
    }
}