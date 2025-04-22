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
using ProyectoFinal_G8.Models.ViewModels; // <-- ViewModel
using System.Security.Claims;

namespace ProyectoFinal_G8.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<CitasController> _logger;

        public CitasController(
            ProyectoFinal_G8Context context,
            UserManager<Usuario> userManager,
            ILogger<CitasController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            string? currentUserIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(currentUserIdStr) || !int.TryParse(currentUserIdStr, out userId))
            {
                _logger.LogError("No se pudo obtener o parsear el ID del usuario logueado.");
                return false;
            }
            return true;
        }

        // GET: Citas o MisCitas
        public async Task<IActionResult> Index()
        {
            if (!TryGetCurrentUserId(out int userIdAsInt))
            {
                return Unauthorized("No se pudo obtener el ID del usuario.");
            }
            ViewData["CurrentUserID"] = userIdAsInt;

            IQueryable<Cita> citasQuery = _context.Citas
                                                .Include(c => c.Mascota)
                                                    .ThenInclude(m => m.Dueño)
                                                .Include(c => c.Veterinario)
                                                .Include(c => c.TipoCita);
            IList<Cita> citas;

            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                citas = await citasQuery.OrderByDescending(c => c.FechaHora).ToListAsync();
                ViewData["VistaTitulo"] = "Gestión de Citas";
            }
            else if (User.IsInRole("Cliente"))
            {
                citas = await citasQuery
                              .Where(c => c.Mascota != null && c.Mascota.IdUsuarioDueño == userIdAsInt)
                              .OrderByDescending(c => c.FechaHora)
                              .ToListAsync();
                ViewData["VistaTitulo"] = "Mis Citas";
            }
            else
            {
                citas = new List<Cita>();
                ViewData["VistaTitulo"] = "Citas";
            }
            return View(citas);
        }

        // GET: Citas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener el ID del usuario.");

            var cita = await _context.Citas
                .Include(c => c.Mascota).ThenInclude(m => m.Dueño)
                .Include(c => c.Veterinario)
                .Include(c => c.TipoCita)
                .FirstOrDefaultAsync(m => m.IdCita == id);

            if (cita == null) { return NotFound(); }

            if (User.IsInRole("Cliente") && cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                TempData["ErrorMessage"] = "No tienes permiso para ver los detalles de esta cita.";
                return RedirectToAction(nameof(Index));
            }

            ViewData["CurrentUserID"] = userIdAsInt;
            return View(cita);
        }

        // GET: Citas/Create
        public async Task<IActionResult> Create()
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener el ID del usuario.");

            await LoadVeterinariosAsync();
            await LoadTiposCitaAsync();

            var viewModel = new CitaCreateViewModel
            {
                FechaHora = DateTime.Now.Date.AddDays(1).AddHours(9)
            };

            if (User.IsInRole("Cliente"))
            {
                ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt);
            }
            else if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                ViewData["MascotasExistentesList"] = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
            }
            else
            {
                return Forbid();
            }

            return View(viewModel);
        }

        // POST: Citas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CitaCreateViewModel viewModel)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener el ID del usuario.");

            if (viewModel.FechaHora <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(viewModel.FechaHora), "La fecha y hora de la cita deben ser en el futuro.");
            }

            if (ModelState.IsValid) // Ejecuta IValidatableObject
            {
                var cita = new Cita
                {
                    FechaHora = viewModel.FechaHora,
                    IdUsuarioVeterinario = viewModel.IdUsuarioVeterinario,
                    IdTipoCita = viewModel.IdTipoCita,
                    Notas = viewModel.Notas,
                    Estado = EstadoCita.Programada
                };

                if (viewModel.RegistrarNuevaMascota)
                {
                    var mascotaParaCita = new Mascota
                    {
                        Nombre = viewModel.NuevoNombreMascota!,
                        Especie = viewModel.NuevaEspecie!,
                        Raza = viewModel.NuevaRaza,
                        FechaNacimiento = viewModel.NuevaFechaNacimiento,
                        IdUsuarioDueño = userIdAsInt
                    };
                    cita.Mascota = mascotaParaCita;
                }
                else
                {
                    bool mascotaValida = await _context.Mascotas
                                                   .AnyAsync(m => m.IdMascota == viewModel.IdMascotaSeleccionada!.Value && m.IdUsuarioDueño == userIdAsInt);
                    if (!mascotaValida)
                    {
                        ModelState.AddModelError(nameof(viewModel.IdMascotaSeleccionada), "La mascota seleccionada no es válida o no te pertenece.");
                        await LoadControlDataForCreateViewAsync(userIdAsInt, viewModel);
                        return View(viewModel);
                    }
                    cita.IdMascota = viewModel.IdMascotaSeleccionada!.Value;
                }

                _context.Add(cita);

                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tu cita ha sido programada correctamente" + (viewModel.RegistrarNuevaMascota ? " y la nueva mascota ha sido registrada." : ".");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException dbEx) { ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar en la base de datos."); _logger.LogError(dbEx, "Error BD Create Cita"); }
                catch (Exception ex) { ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al guardar."); _logger.LogError(ex, "Error General Create Cita"); }
            } // Fin ModelState.IsValid

            _logger.LogWarning($"Create POST (ViewModel): ModelState inválido. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            await LoadControlDataForCreateViewAsync(userIdAsInt, viewModel);
            return View(viewModel);
        }

        // GET: Citas/Edit/5 (Solo Admin/Veterinario)
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _context.Citas.FindAsync(id);
            if (cita == null) { return NotFound(); }

            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            ViewData["IdMascota"] = await GetTodasMascotasSelectListAsync(cita.IdMascota);
            await LoadEstadosCitaAsync(cita.Estado);

            return View(cita);
        }

        // POST: Citas/Edit/5 (Solo Admin/Veterinario)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Estado,Notas")] Cita citaViewModel)
        {
            if (id != citaViewModel.IdCita) { return BadRequest(); }

            ModelState.Remove(nameof(Cita.Mascota));
            ModelState.Remove(nameof(Cita.Veterinario));
            ModelState.Remove(nameof(Cita.TipoCita));

            if (!EstadoCita.GetEstadosEditables().Contains(citaViewModel.Estado ?? ""))
            {
                ModelState.AddModelError(nameof(Cita.Estado), "El estado seleccionado no es válido.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(citaViewModel);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cita actualizada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!await CitaExists(citaViewModel.IdCita)) { return NotFound(); }
                    else { ModelState.AddModelError(string.Empty, "Conflicto de concurrencia."); _logger.LogError(ex, "Concurrency Error Edit Admin"); }
                }
                catch (Exception ex) { ModelState.AddModelError(string.Empty, "Error al actualizar."); _logger.LogError(ex, "General Error Edit Admin"); }
            }

            await LoadVeterinariosAsync(citaViewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(citaViewModel.IdTipoCita);
            ViewData["IdMascota"] = await GetTodasMascotasSelectListAsync(citaViewModel.IdMascota);
            await LoadEstadosCitaAsync(citaViewModel.Estado);
            return View(citaViewModel);
        }

        // --- Edición para Cliente ---

        // GET: Citas/EditCliente/5
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> EditCliente(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized(); }

            var cita = await _context.Citas
                             .Include(c => c.Mascota)
                             .FirstOrDefaultAsync(c => c.IdCita == id);

            if (cita == null) { return NotFound(); }

            string? errorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt) { errorMessage = "No tienes permiso."; }
            else if (cita.Estado != EstadoCita.Programada) { errorMessage = "Estado no permite modificación."; }
            else if (cita.FechaHora <= DateTime.UtcNow) { errorMessage = "Fecha pasada."; }

            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction(nameof(Index));
            }

            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            // *** Cargar datos para el dropdown de EditCliente.cshtml ***
            ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt, cita.IdMascota);

            return View("EditCliente", cita);
        }

        // POST: Citas/EditCliente/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> EditCliente(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Notas")] Cita citaViewModel)
        {
            if (id != citaViewModel.IdCita) { return BadRequest(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized(); }

            var citaOriginal = await _context.Citas
                                     .Include(c => c.Mascota)
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(c => c.IdCita == id);

            if (citaOriginal == null) { return NotFound(); }

            string? redirectErrorMessage = null;
            if (citaOriginal.Mascota?.IdUsuarioDueño != userIdAsInt) { redirectErrorMessage = "No tienes permiso."; }
            else if (citaOriginal.Estado != EstadoCita.Programada) { redirectErrorMessage = "Estado cambió."; }
            else if (citaOriginal.FechaHora <= DateTime.UtcNow) { redirectErrorMessage = "Fecha original pasada."; }

            if (redirectErrorMessage != null)
            {
                TempData["ErrorMessage"] = redirectErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            ModelState.Remove(nameof(Cita.Mascota));
            ModelState.Remove(nameof(Cita.Veterinario));
            ModelState.Remove(nameof(Cita.TipoCita));
            ModelState.Remove(nameof(Cita.Estado));

            if (citaViewModel.FechaHora <= DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(Cita.FechaHora), "La nueva fecha debe ser futura.");
            }

            bool mascotaSeleccionadaValida = await _context.Mascotas
                                                     .AnyAsync(m => m.IdMascota == citaViewModel.IdMascota && m.IdUsuarioDueño == userIdAsInt);
            if (!mascotaSeleccionadaValida)
            {
                ModelState.AddModelError(nameof(Cita.IdMascota), "La mascota no es válida.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var citaParaActualizar = await _context.Citas
                                                   .Include(c => c.Mascota)
                                                   .FirstOrDefaultAsync(c => c.IdCita == id);
                    if (citaParaActualizar == null) return NotFound();

                    if (citaParaActualizar.Mascota?.IdUsuarioDueño != userIdAsInt || citaParaActualizar.Estado != EstadoCita.Programada)
                    {
                        TempData["ErrorMessage"] = "La cita ya no cumple las condiciones.";
                        return RedirectToAction(nameof(Index));
                    }

                    citaParaActualizar.FechaHora = citaViewModel.FechaHora;
                    citaParaActualizar.IdMascota = citaViewModel.IdMascota;
                    citaParaActualizar.IdUsuarioVeterinario = citaViewModel.IdUsuarioVeterinario;
                    citaParaActualizar.IdTipoCita = citaViewModel.IdTipoCita;
                    citaParaActualizar.Notas = citaViewModel.Notas;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tu cita ha sido modificada.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex) { ModelState.AddModelError(string.Empty, "Conflicto concurrencia."); _logger.LogError(ex, "Concurrency Error EditCliente"); }
                catch (Exception ex) { ModelState.AddModelError(string.Empty, "Error al modificar."); _logger.LogError(ex, "General Error EditCliente"); }
            }

            // Recargar datos si falla
            await LoadVeterinariosAsync(citaViewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(citaViewModel.IdTipoCita);
            // *** Cargar datos para el dropdown de EditCliente.cshtml ***
            ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt, citaViewModel.IdMascota);
            return View("EditCliente", citaViewModel);
        }


        // --- Cancelación por Cliente ---

        // GET: Citas/CancelCliente/5
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CancelCliente(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized(); }

            var cita = await _context.Citas
                             .Include(c => c.Mascota).Include(c => c.Veterinario).Include(c => c.TipoCita)
                             .FirstOrDefaultAsync(c => c.IdCita == id);
            if (cita == null) { return NotFound(); }

            string? errorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt) { errorMessage = "No tienes permiso."; }
            else if (cita.Estado != EstadoCita.Programada) { errorMessage = "Solo puedes cancelar 'Programada'."; }
            else if (cita.FechaHora <= DateTime.UtcNow) { errorMessage = "No puedes cancelar cita pasada."; }

            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction(nameof(Index));
            }
            return View("CancelCliente", cita);
        }

        // POST: Citas/CancelCliente/5 (Confirmación)
        [HttpPost, ActionName("CancelCliente")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CancelClienteConfirmed(int id)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized(); }
            var cita = await _context.Citas.Include(c => c.Mascota).FirstOrDefaultAsync(c => c.IdCita == id);
            if (cita == null) { return NotFound(); }

            string? redirectErrorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt) { redirectErrorMessage = "No tienes permiso."; }
            else if (cita.Estado != EstadoCita.Programada) { redirectErrorMessage = "Estado ha cambiado."; }
            else if (cita.FechaHora <= DateTime.UtcNow) { redirectErrorMessage = "Fecha ya pasó."; }

            if (redirectErrorMessage != null)
            {
                TempData["ErrorMessage"] = redirectErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                cita.Estado = EstadoCita.CanceladaCliente;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tu cita ha sido cancelada.";
            }
            catch (Exception ex) { TempData["ErrorMessage"] = "Error al cancelar."; _logger.LogError(ex, "Error CancelClienteConfirmed"); }

            return RedirectToAction(nameof(Index));
        }


        // --- Borrado (Solo Admin/Veterinario) ---

        // GET: Citas/Delete/5
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _context.Citas
                .Include(c => c.Mascota).ThenInclude(m => m.Dueño)
                .Include(c => c.Veterinario).Include(c => c.TipoCita)
                .FirstOrDefaultAsync(m => m.IdCita == id);
            if (cita == null) { return NotFound(); }
            return View(cita);
        }

        // POST: Citas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cita = await _context.Citas.FindAsync(id);
            if (cita != null)
            {
                try
                {
                    _context.Citas.Remove(cita);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cita eliminada.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error al eliminar: " + ex.Message;
                    _logger.LogError(ex, "Error DeleteConfirmed");
                    return RedirectToAction(nameof(Delete), new { id = id });
                }
            }
            else { TempData["ErrorMessage"] = "Cita no encontrada."; }
            return RedirectToAction(nameof(Index));
        }

        // --- Métodos Auxiliares ---

        // Helper unificado para recargar datos para la vista Create
        private async Task LoadControlDataForCreateViewAsync(int userIdAsInt, CitaCreateViewModel viewModel)
        {
            await LoadVeterinariosAsync(viewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(viewModel.IdTipoCita);
            ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt, viewModel.IdMascotaSeleccionada);
        }

        private async Task LoadVeterinariosAsync(object? selectedVeterinario = null)
        {
            var veterinarios = await _userManager.GetUsersInRoleAsync("Veterinario");
            var veterinarioList = veterinarios.Select(u => new { u.Id, NombreCompleto = u.Nombre }).OrderBy(u => u.NombreCompleto).ToList();
            ViewData["IdUsuarioVeterinario"] = new SelectList(veterinarioList, "Id", "NombreCompleto", selectedVeterinario);
        }

        // Retorna SelectList para mascotas del cliente
        private async Task<SelectList> GetMisMascotasSelectListAsync(int currentUserId, object? selectedMascota = null)
        {
            var misMascotas = await _context.Mascotas
                                          .Where(m => m.IdUsuarioDueño == currentUserId)
                                          .OrderBy(m => m.Nombre)
                                          .Select(m => new { m.IdMascota, m.Nombre })
                                          .ToListAsync();
            _logger.LogDebug($"GetMisMascotasSelectListAsync: Encontradas {misMascotas.Count} mascotas para cliente ID: {currentUserId}");
            return new SelectList(misMascotas, "IdMascota", "Nombre", selectedMascota);
        }

        // Retorna SelectList para todas las mascotas (Admin/Vet)
        private async Task<SelectList> GetTodasMascotasSelectListAsync(object? selectedMascota = null)
        {
            // 1. Traer las Mascotas (con Dueño) a memoria
            var todasMascotas = await _context.Mascotas
                                            .Include(m => m.Dueño) // Incluir dueño
                                            .OrderBy(m => m.Nombre)
                                            .ToListAsync(); // Ejecuta la consulta SQL

            // 2. Proyectar la lista en memoria para crear los SelectListItem
            var mascotaSelectListItems = todasMascotas.Select(m => new SelectListItem
            {
                Value = m.IdMascota.ToString(),
                // El operador ?. SÍ se puede usar aquí (LINQ to Objects)
                Text = $"{m.Nombre} (Dueño: {m.Dueño?.Nombre ?? "N/A"})"
            }).ToList();

            _logger.LogDebug($"GetTodasMascotasSelectListAsync: Cargadas {todasMascotas.Count} mascotas totales.");

            // 3. Crear y devolver el SelectList final
            string? selectedValue = selectedMascota?.ToString();
            return new SelectList(mascotaSelectListItems, "Value", "Text", selectedValue);
        }


        private async Task LoadTiposCitaAsync(object? selectedTipo = null)
        {
            var tipos = await _context.TiposCita.OrderBy(t => t.Nombre).Select(t => new { t.IdTipoCita, t.Nombre }).ToListAsync();
            ViewData["IdTipoCita"] = new SelectList(tipos, "IdTipoCita", "Nombre", selectedTipo);
        }

        private Task LoadEstadosCitaAsync(object? selectedEstado = null)
        {
            var estadosDisponibles = EstadoCita.GetEstadosEditables().Select(e => new SelectListItem { Value = e, Text = e }).ToList();
            ViewData["EstadosCita"] = new SelectList(estadosDisponibles, "Value", "Text", selectedEstado);
            return Task.CompletedTask;
        }

        private async Task<bool> CitaExists(int id)
        {
            return await _context.Citas.AnyAsync(e => e.IdCita == id);
        }

    }
}