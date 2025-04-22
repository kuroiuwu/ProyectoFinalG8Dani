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
using ProyectoFinal_G8.Models.ViewModels; 
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace ProyectoFinal_G8.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<CitasController> _logger;

        // Constantes para horas de citas
        private const int MinAppointmentHour = 9;  // 9 AM
        private const int MaxAppointmentHour = 17; // 5 PM
        private bool isAdminOrVet => User.IsInRole("Admin") || User.IsInRole("Veterinario"); // Helper property

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
            { _logger.LogError("No se pudo obtener ID usuario."); return false; }
            return true;
        }

        // GET: Citas/Index (Con filtros)
        public async Task<IActionResult> Index(DateTime? filterDate, string? filterStatus, int? filterMascotaId, int? filterDuenoId)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener ID usuario."); }
            ViewData["CurrentUserID"] = userIdAsInt;

            if (isAdminOrVet)
            { /* Cargar ViewData para filtros */
                ViewData["CurrentFilterDate"] = filterDate?.ToString("yyyy-MM-dd");
                ViewData["CurrentFilterStatus"] = filterStatus;
                ViewData["CurrentFilterMascotaId"] = filterMascotaId;
                ViewData["CurrentFilterDuenoId"] = filterDuenoId;
                ViewData["StatusList"] = GetStatusSelectList(filterStatus);
                ViewData["MascotaList"] = await GetMascotaFilterSelectList(filterMascotaId);
                ViewData["DuenoList"] = await GetDuenoFilterSelectList(filterDuenoId);
            }

            IQueryable<Cita> citasQuery = _context.Citas
                                            .Include(c => c.Mascota)
                                               .ThenInclude(m => m!.Dueño)
                                            .Include(c => c.Veterinario)
                                            .Include(c => c.TipoCita);

            if (isAdminOrVet)
            { /* Aplicar filtros generales */
                if (filterDate.HasValue) { citasQuery = citasQuery.Where(c => c.FechaHora.Date == filterDate.Value.Date); }
                if (!string.IsNullOrEmpty(filterStatus)) { citasQuery = citasQuery.Where(c => c.Estado == filterStatus); }
                if (filterMascotaId.HasValue) { citasQuery = citasQuery.Where(c => c.IdMascota == filterMascotaId.Value); }
                if (filterDuenoId.HasValue) { citasQuery = citasQuery.Where(c => c.Mascota != null && c.Mascota.IdUsuarioDueño == filterDuenoId.Value); }
                ViewData["VistaTitulo"] = "Gestión de Citas";
            }
            else if (User.IsInRole("Cliente"))
            { /* Filtro Cliente */
                citasQuery = citasQuery.Where(c => c.Mascota != null && c.Mascota.IdUsuarioDueño == userIdAsInt);
                ViewData["VistaTitulo"] = "Mis Citas";
            }
            else
            {
                citasQuery = citasQuery.Where(c => false);
                ViewData["VistaTitulo"] = "Citas";
            }

            var citas = await citasQuery.OrderByDescending(c => c.FechaHora).ToListAsync();

            /* Lógica de Auto-Update Status */
            var now = DateTime.UtcNow;
            var idsToUpdate = new List<int>();
            bool changesMade = false;
            foreach (var cita in citas)
            {
                if ((cita.Estado == EstadoCita.Programada || cita.Estado == EstadoCita.Confirmada) && now > cita.FechaHora.ToUniversalTime().AddHours(1))
                {
                    idsToUpdate.Add(cita.IdCita);
                    cita.Estado = EstadoCita.Realizada;
                    changesMade = true;
                }
            }
            if (changesMade && idsToUpdate.Any())
            {
                try
                {
                    await _context.Citas
                        .Where(c => idsToUpdate.Contains(c.IdCita))
                        .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, EstadoCita.Realizada));
                    _logger.LogInformation($"Actualizadas {idsToUpdate.Count} citas al estado '{EstadoCita.Realizada}'.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-update status.");
                    TempData["ErrorMessage"] = "Error actualizando estados de citas pasadas.";
                }
            }
            /* Fin Lógica Auto-Update */

            return View(citas);
        }


        // GET: Citas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID usuario.");

            var cita = await _context.Citas
                           .Include(c => c.Mascota)
                              .ThenInclude(m => m!.Dueño)
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
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID usuario.");
            await LoadVeterinariosAsync();
            await LoadTiposCitaAsync();

            var viewModel = new CitaCreateViewModel { SelectedDate = DateTime.Today.AddDays(1) };

            if (User.IsInRole("Cliente"))
            {
                ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt);
            }
            else if (isAdminOrVet)
            {
                ViewData["MascotasExistentesList"] = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");
            }
            else { return Forbid(); }

            return View(viewModel);
        }

        // POST: Citas/Create 
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CitaCreateViewModel viewModel)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener ID usuario.");

            DateTime constructedFechaHora = DateTime.MinValue;
            
            if (TimeSpan.TryParseExact(viewModel.SelectedTime, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan timeOfDay))
            {
                constructedFechaHora = viewModel.SelectedDate.Date.Add(timeOfDay);
                if (constructedFechaHora <= DateTime.Now) { ModelState.AddModelError(nameof(viewModel.SelectedTime), "La fecha y hora seleccionadas deben ser en el futuro."); }
                if (constructedFechaHora.Hour < MinAppointmentHour || constructedFechaHora.Hour > MaxAppointmentHour || constructedFechaHora.Minute != 0)
                {
                    ModelState.AddModelError(nameof(viewModel.SelectedTime), $"La hora debe ser exacta (ej: 9:00) y estar entre las {MinAppointmentHour}:00 y las {MaxAppointmentHour}:00.");
                }
            }
            else if (!string.IsNullOrEmpty(viewModel.SelectedTime))
            {
                // Mantenemos el mensaje de error original por si el formato es completamente inválido
                ModelState.AddModelError(nameof(viewModel.SelectedTime), "El formato de la hora seleccionada no es válido (esperado hh:mm).");
            }

            TryValidateModel(viewModel);

            if (ModelState.IsValid && constructedFechaHora > DateTime.MinValue)
            {
                bool slotTaken = await _context.Citas
                    .AnyAsync(c => c.FechaHora == constructedFechaHora &&
                                   c.Estado != EstadoCita.CanceladaCliente &&
                                   c.Estado != EstadoCita.CanceladaStaff);
                if (slotTaken)
                {
                    ModelState.AddModelError(string.Empty, "Lo sentimos, este horario acaba de ser reservado. Por favor, seleccione otro.");
                    _logger.LogWarning("Intento de doble reserva fallido para la hora: {FechaHora}", constructedFechaHora);
                }
            }

            if (ModelState.IsValid)
            {
                var cita = new Cita
                {
                    FechaHora = constructedFechaHora,
                    IdUsuarioVeterinario = viewModel.IdUsuarioVeterinario,
                    IdTipoCita = viewModel.IdTipoCita,
                    Notas = viewModel.Notas,
                    Estado = EstadoCita.Programada
                };

                /* Lógica Mascota */
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
                    bool mascotaValida = false;
                    if (viewModel.IdMascotaSeleccionada.HasValue)
                    {
                        mascotaValida = await _context.Mascotas
                           .AnyAsync(m => m.IdMascota == viewModel.IdMascotaSeleccionada.Value && m.IdUsuarioDueño == userIdAsInt);
                    }

                    if (!mascotaValida && User.IsInRole("Cliente"))
                    {
                        ModelState.AddModelError(nameof(viewModel.IdMascotaSeleccionada), "La mascota seleccionada no es válida o no te pertenece (verificación final).");
                        await LoadControlDataForCreateViewAsync(userIdAsInt, viewModel);
                        return View(viewModel);
                    }
                    if (viewModel.IdMascotaSeleccionada.HasValue)
                    {
                        cita.IdMascota = viewModel.IdMascotaSeleccionada.Value;
                    }
                    else
                    {
                        ModelState.AddModelError(nameof(viewModel.IdMascotaSeleccionada), "Debe seleccionar una mascota existente si no registra una nueva.");
                        await LoadControlDataForCreateViewAsync(userIdAsInt, viewModel);
                        return View(viewModel);
                    }
                }
                /* Fin Lógica Mascota */

                _context.Add(cita);
                try
                {
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Tu cita ha sido programada correctamente" + (viewModel.RegistrarNuevaMascota ? " y la nueva mascota ha sido registrada." : ".");
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException dbEx)
                {
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar en la base de datos.");
                    _logger.LogError(dbEx, "Error BD Create Cita");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al guardar.");
                    _logger.LogError(ex, "Error General Create Cita");
                }
            } // Fin ModelState.IsValid

            _logger.LogWarning($"Create POST (ViewModel): ModelState inválido. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            await LoadControlDataForCreateViewAsync(userIdAsInt, viewModel);
            return View(viewModel);
        }

        // GET: Citas/Edit/5 (Admin/Vet - Usa ViewModel)
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var cita = await _context.Citas
                                      .Include(c => c.Mascota)
                                         .ThenInclude(m => m!.Dueño)
                                      .Include(c => c.Veterinario)
                                      .FirstOrDefaultAsync(c => c.IdCita == id);

            if (cita == null) { return NotFound(); }

            var viewModel = new CitaEditViewModel
            {
                IdCita = cita.IdCita,
                SelectedDate = cita.FechaHora.Date,
                SelectedTime = cita.FechaHora.ToString("HH:mm"), 
                IdMascota = cita.IdMascota,
                IdUsuarioVeterinario = cita.IdUsuarioVeterinario,
                IdTipoCita = cita.IdTipoCita,
                Estado = cita.Estado,
                Notas = cita.Notas,
                MascotaNombre = cita.Mascota?.Nombre,
                DuenoNombre = cita.Mascota?.Dueño?.Nombre ?? cita.Mascota?.Dueño?.UserName
            };

            await LoadVeterinariosAsync(viewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(viewModel.IdTipoCita);
            ViewData["IdMascota"] = await GetTodasMascotasSelectListAsync(viewModel.IdMascota);
            await LoadEstadosCitaAsync(viewModel.Estado);

            return View(viewModel);
        }

        // POST: Citas/Edit/5 (Admin/Vet - AJUSTADO el formato de hora en TryParseExact)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int id, CitaEditViewModel viewModel)
        {
            if (id != viewModel.IdCita) { return BadRequest(); }

            DateTime constructedFechaHora = DateTime.MinValue;
            
            if (TimeSpan.TryParseExact(viewModel.SelectedTime, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan timeOfDay))
            {
                constructedFechaHora = viewModel.SelectedDate.Date.Add(timeOfDay);
                
            }
            else if (!string.IsNullOrEmpty(viewModel.SelectedTime))
            {
                ModelState.AddModelError(nameof(viewModel.SelectedTime), "Formato hora inválido (esperado hh:mm).");
            }

            if (!EstadoCita.GetEstadosEditables().Contains(viewModel.Estado ?? ""))
            {
                ModelState.AddModelError(nameof(viewModel.Estado), "El estado seleccionado no es válido.");
            }

            if (ModelState.IsValid && constructedFechaHora > DateTime.MinValue)
            {
                bool slotTakenByOther = await _context.Citas
                    .AnyAsync(c => c.FechaHora == constructedFechaHora
                                   && c.IdCita != viewModel.IdCita
                                   && c.Estado != EstadoCita.CanceladaCliente
                                   && c.Estado != EstadoCita.CanceladaStaff);
                if (slotTakenByOther)
                {
                    ModelState.AddModelError(nameof(viewModel.SelectedTime), "Este horario ya está ocupado por otra cita.");
                    _logger.LogWarning("Admin/Vet intentó mover cita {IdCita} a horario ocupado {FechaHora}", viewModel.IdCita, constructedFechaHora);
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var citaToUpdate = await _context.Citas.FindAsync(viewModel.IdCita);
                    if (citaToUpdate == null) { return NotFound(); }

                    citaToUpdate.FechaHora = constructedFechaHora;
                    citaToUpdate.IdMascota = viewModel.IdMascota;
                    citaToUpdate.IdUsuarioVeterinario = viewModel.IdUsuarioVeterinario;
                    citaToUpdate.IdTipoCita = viewModel.IdTipoCita;
                    citaToUpdate.Estado = viewModel.Estado;
                    citaToUpdate.Notas = viewModel.Notas;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cita actualizada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (!await CitaExists(viewModel.IdCita)) { return NotFound(); }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "La cita fue modificada por otro usuario. Intente de nuevo.");
                        _logger.LogError(ex, "Concurrency Error Edit Admin/Vet Cita ID {CitaId}", viewModel.IdCita);
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    ModelState.AddModelError(string.Empty, "Error al guardar en la base de datos.");
                    _logger.LogError(dbEx, "DB Error Edit Admin/Vet Cita ID {CitaId}", viewModel.IdCita);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al actualizar.");
                    _logger.LogError(ex, "General Error Edit Admin/Vet Cita ID {CitaId}", viewModel.IdCita);
                }
            }

            _logger.LogWarning($"Edit POST (Admin/Vet) inválido para Cita ID {id}. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            await LoadVeterinariosAsync(viewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(viewModel.IdTipoCita);
            ViewData["IdMascota"] = await GetTodasMascotasSelectListAsync(viewModel.IdMascota);
            await LoadEstadosCitaAsync(viewModel.Estado);
            viewModel.MascotaNombre = await _context.Mascotas.Where(m => m.IdMascota == viewModel.IdMascota).Select(m => m.Nombre).FirstOrDefaultAsync();
            var dueño = await _context.Mascotas.Where(m => m.IdMascota == viewModel.IdMascota).Select(m => m.Dueño).FirstOrDefaultAsync();
            viewModel.DuenoNombre = dueño?.Nombre ?? dueño?.UserName;

            return View(viewModel);
        }


        // GET: Citas/EditCliente/5 (Usa ViewModel)
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> EditCliente(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            var cita = await _context.Citas
                                      .Include(c => c.Mascota)
                                      .FirstOrDefaultAsync(c => c.IdCita == id);

            if (cita == null) { return NotFound(); }

            string? errorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt) { errorMessage = "No tienes permiso."; }
            else if (cita.Estado != EstadoCita.Programada) { errorMessage = $"Solo puedes modificar citas '{EstadoCita.Programada}'."; }
            else if (cita.FechaHora <= DateTime.Now) { errorMessage = "No puedes modificar citas pasadas."; }

            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
                _logger.LogWarning("GET EditCliente fallido Cita ID {CitaId} User ID {UserId}. Razón: {Reason}", id, userIdAsInt, errorMessage);
                return RedirectToAction(nameof(Index));
            }

            var viewModel = new CitaEditClienteViewModel
            {
                IdCita = cita.IdCita,
                SelectedDate = cita.FechaHora.Date,
                SelectedTime = cita.FechaHora.ToString("HH:mm"), 
                IdMascota = cita.IdMascota,
                IdUsuarioVeterinario = cita.IdUsuarioVeterinario,
                IdTipoCita = cita.IdTipoCita,
                Notas = cita.Notas,
                MascotaNombre = cita.Mascota?.Nombre
            };

            await LoadVeterinariosAsync(viewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(viewModel.IdTipoCita);
            ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt, viewModel.IdMascota);

            return View("EditCliente", viewModel);
        }

        // POST: Citas/EditCliente/5 (AJUSTADO el formato de hora en TryParseExact)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> EditCliente(int id, CitaEditClienteViewModel viewModel)
        {
            if (id != viewModel.IdCita) { return BadRequest(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            DateTime constructedFechaHora = DateTime.MinValue;
            // ***** AJUSTE CLAVE AQUÍ: Usamos "hh\\:mm" como en la versión funcional *****
            if (TimeSpan.TryParseExact(viewModel.SelectedTime, "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan timeOfDay))
            {
                constructedFechaHora = viewModel.SelectedDate.Date.Add(timeOfDay);
                if (constructedFechaHora <= DateTime.Now)
                {
                    ModelState.AddModelError(nameof(viewModel.SelectedTime), "La nueva fecha y hora deben ser en el futuro.");
                }
                if (constructedFechaHora.Hour < MinAppointmentHour || constructedFechaHora.Hour > MaxAppointmentHour || constructedFechaHora.Minute != 0)
                {
                    ModelState.AddModelError(nameof(viewModel.SelectedTime), $"La hora debe ser exacta (ej: 9:00) y estar entre {MinAppointmentHour}:00-{MaxAppointmentHour}:00.");
                }
            }
            else if (!string.IsNullOrEmpty(viewModel.SelectedTime))
            {
                ModelState.AddModelError(nameof(viewModel.SelectedTime), "El formato de la hora no es válido (esperado hh:mm).");
            }

            bool mascotaSeleccionadaValida = false;
            if (viewModel.IdMascota > 0)
            {
                mascotaSeleccionadaValida = await _context.Mascotas
                   .AnyAsync(m => m.IdMascota == viewModel.IdMascota && m.IdUsuarioDueño == userIdAsInt);
            }
            if (!mascotaSeleccionadaValida)
            {
                ModelState.AddModelError(nameof(viewModel.IdMascota), "La mascota seleccionada no es válida o no te pertenece.");
            }

            var citaOriginal = await _context.Citas
                                       .Include(c => c.Mascota)
                                       .AsNoTracking()
                                       .FirstOrDefaultAsync(c => c.IdCita == id);

            if (citaOriginal == null) { return NotFound(); }

            string? redirectErrorMessage = null;
            if (citaOriginal.Mascota?.IdUsuarioDueño != userIdAsInt) { redirectErrorMessage = "No tienes permiso (verificación final)."; }
            else if (citaOriginal.Estado != EstadoCita.Programada) { redirectErrorMessage = "Estado cambió."; }
            else if (citaOriginal.FechaHora <= DateTime.Now) { redirectErrorMessage = "Fecha original ya pasó."; }

            if (redirectErrorMessage != null)
            {
                TempData["ErrorMessage"] = redirectErrorMessage;
                _logger.LogWarning("POST EditCliente fallido (concurrencia/permiso) Cita ID {CitaId} User ID {UserId}. Razón: {Reason}", id, userIdAsInt, redirectErrorMessage);
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid && constructedFechaHora > DateTime.MinValue)
            {
                bool slotTakenByOther = await _context.Citas
                    .AnyAsync(c => c.FechaHora == constructedFechaHora
                                   && c.IdCita != viewModel.IdCita
                                   && c.Estado != EstadoCita.CanceladaCliente
                                   && c.Estado != EstadoCita.CanceladaStaff);
                if (slotTakenByOther)
                {
                    ModelState.AddModelError(nameof(viewModel.SelectedTime), "Este horario ya está ocupado.");
                    _logger.LogWarning("Cliente {UserId} intentó mover cita {IdCita} a horario ocupado {FechaHora}", userIdAsInt, viewModel.IdCita, constructedFechaHora);
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var citaToUpdate = await _context.Citas
                                            .Include(c => c.Mascota)
                                            .FirstOrDefaultAsync(c => c.IdCita == id);

                    if (citaToUpdate == null) return NotFound();

                    if (citaToUpdate.Mascota?.IdUsuarioDueño != userIdAsInt || citaToUpdate.Estado != EstadoCita.Programada)
                    {
                        TempData["ErrorMessage"] = "Cita ya no modificable.";
                        return RedirectToAction(nameof(Index));
                    }

                    citaToUpdate.FechaHora = constructedFechaHora;
                    citaToUpdate.IdMascota = viewModel.IdMascota;
                    citaToUpdate.IdUsuarioVeterinario = viewModel.IdUsuarioVeterinario;
                    citaToUpdate.IdTipoCita = viewModel.IdTipoCita;
                    citaToUpdate.Notas = viewModel.Notas;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cita modificada.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex) { ModelState.AddModelError("", "Conflicto concurrencia."); _logger.LogError(ex, "Concurrency Error EditCliente"); }
                catch (DbUpdateException dbEx) { ModelState.AddModelError("", "Error BD."); _logger.LogError(dbEx, "DB Error EditCliente"); }
                catch (Exception ex) { ModelState.AddModelError("", "Error inesperado."); _logger.LogError(ex, "General Error EditCliente"); }
            }

            _logger.LogWarning($"EditCliente POST inválido: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            await LoadVeterinariosAsync(viewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(viewModel.IdTipoCita);
            ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt, viewModel.IdMascota);
            viewModel.MascotaNombre = await _context.Mascotas.Where(m => m.IdMascota == viewModel.IdMascota).Select(m => m.Nombre).FirstOrDefaultAsync();

            return View("EditCliente", viewModel);
        }


        // GET: Citas/CancelCliente/5
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CancelCliente(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized(); }
            var cita = await _context.Citas.Include(c => c.Mascota).Include(c => c.Veterinario).Include(c => c.TipoCita).FirstOrDefaultAsync(c => c.IdCita == id);
            if (cita == null) { return NotFound(); }
            string? errorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt) { errorMessage = "No tienes permiso."; }
            else if (cita.Estado != EstadoCita.Programada && cita.Estado != EstadoCita.Confirmada) { errorMessage = $"Solo puedes cancelar '{EstadoCita.Programada}' o '{EstadoCita.Confirmada}'."; }
            else if (cita.FechaHora <= DateTime.Now) { errorMessage = "Fecha pasada."; }
            if (errorMessage != null) { TempData["ErrorMessage"] = errorMessage; return RedirectToAction(nameof(Index)); }
            return View("CancelCliente", cita);
        }

        // POST: Citas/CancelCliente/5
        [HttpPost, ActionName("CancelCliente")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CancelClienteConfirmed(int id)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized(); }
            var cita = await _context.Citas.Include(c => c.Mascota).FirstOrDefaultAsync(c => c.IdCita == id);
            if (cita == null) { return NotFound(); }
            string? redirectErrorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt) { redirectErrorMessage = "No tienes permiso (verificación final)."; }
            else if (cita.Estado != EstadoCita.Programada && cita.Estado != EstadoCita.Confirmada) { redirectErrorMessage = "Estado ha cambiado."; }
            else if (cita.FechaHora <= DateTime.Now) { redirectErrorMessage = "Fecha ya pasó."; }
            if (redirectErrorMessage != null) { TempData["ErrorMessage"] = redirectErrorMessage; return RedirectToAction(nameof(Index)); }
            try
            {
                cita.Estado = EstadoCita.CanceladaCliente;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cita cancelada.";
            }
            catch (DbUpdateConcurrencyException ex) { TempData["ErrorMessage"] = "Error de concurrencia al cancelar."; _logger.LogError(ex, "Concurrency Error CancelClienteConfirmed"); }
            catch (Exception ex) { TempData["ErrorMessage"] = "Error al cancelar."; _logger.LogError(ex, "Error CancelClienteConfirmed"); }
            return RedirectToAction(nameof(Index));
        }

        // GET: Citas/Delete/5
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var cita = await _context.Citas.Include(c => c.Mascota).ThenInclude(m => m!.Dueño).Include(c => c.Veterinario).Include(c => c.TipoCita).FirstOrDefaultAsync(m => m.IdCita == id);
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
                catch (DbUpdateException dbEx)
                {
                    TempData["ErrorMessage"] = "Error al eliminar (dependencias?).";
                    _logger.LogError(dbEx, "DB Error Deleting Cita ID {CitaId}", id);
                    return RedirectToAction(nameof(Delete), new { id = id, saveChangesError = true }); // Pasar error a la vista
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Error inesperado.";
                    _logger.LogError(ex, "General Error Deleting Cita ID {CitaId}", id);
                    return RedirectToAction(nameof(Delete), new { id = id, saveChangesError = true }); // Pasar error a la vista
                }
            }
            else { TempData["ErrorMessage"] = "Cita no encontrada."; }
            return RedirectToAction(nameof(Index));
        }


        // --- Métodos Auxiliares ---

        private async Task LoadControlDataForCreateViewAsync(int userIdAsInt, CitaCreateViewModel viewModel)
        {
            await LoadVeterinariosAsync(viewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(viewModel.IdTipoCita);
            if (User.IsInRole("Cliente")) { ViewData["MascotasExistentesList"] = await GetMisMascotasSelectListAsync(userIdAsInt, viewModel.IdMascotaSeleccionada); }
            else if (isAdminOrVet) { ViewData["MascotasExistentesList"] = new SelectList(Enumerable.Empty<SelectListItem>()); }
        }
        private async Task LoadVeterinariosAsync(object? selectedVeterinario = null)
        {
            var v = await _userManager.GetUsersInRoleAsync("Veterinario");
            var l = v.Select(u => new { Id = u.Id, NombreCompleto = u.Nombre ?? u.UserName }).OrderBy(u => u.NombreCompleto).ToList();
            ViewData["IdUsuarioVeterinario"] = new SelectList(l, "Id", "NombreCompleto", selectedVeterinario);
        }
        private async Task<SelectList> GetMisMascotasSelectListAsync(int currentUserId, object? selectedMascota = null)
        {
            var m = await _context.Mascotas.Where(m => m.IdUsuarioDueño == currentUserId).OrderBy(m => m.Nombre).Select(m => new { m.IdMascota, m.Nombre }).ToListAsync();
            return new SelectList(m, "IdMascota", "Nombre", selectedMascota);
        }
        private async Task<SelectList> GetTodasMascotasSelectListAsync(object? selectedMascota = null)
        {
            var t = await _context.Mascotas.Include(m => m.Dueño).OrderBy(m => m.Nombre).ToListAsync();
            var i = t.Select(m => new SelectListItem { Value = m.IdMascota.ToString(), Text = $"{m.Nombre} (Dueño: {m.Dueño?.Nombre ?? m.Dueño?.UserName ?? "N/A"})" }).ToList();
            return new SelectList(i, "Value", "Text", selectedMascota?.ToString());
        }
        private async Task LoadTiposCitaAsync(object? selectedTipo = null)
        {
            var t = await _context.TiposCita.OrderBy(t => t.Nombre).Select(t => new { t.IdTipoCita, t.Nombre }).ToListAsync();
            ViewData["IdTipoCita"] = new SelectList(t, "IdTipoCita", "Nombre", selectedTipo);
        }
        private Task LoadEstadosCitaAsync(object? selectedEstado = null)
        {
            var e = EstadoCita.GetEstadosEditables().Select(e => new SelectListItem { Value = e, Text = e }).OrderBy(i => i.Text).ToList();
            ViewData["EstadosCita"] = new SelectList(e, "Value", "Text", selectedEstado);
            return Task.CompletedTask;
        }
        private SelectList GetStatusSelectList(string? selectedStatus)
        {
            var a = typeof(EstadoCita).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy).Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string)).Select(fi => (string?)fi.GetRawConstantValue()).Where(s => s != null).OrderBy(s => s).ToList();
            var i = a.Select(status => new SelectListItem { Value = status, Text = status }).ToList();
            i.Insert(0, new SelectListItem { Value = "", Text = "Todos los Estados" });
            return new SelectList(i, "Value", "Text", selectedStatus);
        }
        private async Task<SelectList> GetMascotaFilterSelectList(int? selectedMascotaId)
        {
            var m = await _context.Mascotas.Include(m => m.Dueño).OrderBy(m => m.Nombre).Select(m => new { Id = m.IdMascota, DisplayText = $"{m.Nombre} ({m.Dueño!.Nombre ?? m.Dueño.UserName ?? "Sin Dueño"})" }).ToListAsync();
            var i = m.Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.DisplayText }).ToList();
            i.Insert(0, new SelectListItem { Value = "", Text = "Todas las Mascotas" });
            return new SelectList(i, "Value", "Text", selectedMascotaId?.ToString());
        }
        private async Task<SelectList> GetDuenoFilterSelectList(int? selectedDuenoId)
        {
            var c = await _userManager.GetUsersInRoleAsync("Cliente");
            var i = c.OrderBy(u => u.Nombre ?? u.UserName).Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Nombre ?? u.UserName ?? $"ID: {u.Id}" }).ToList();
            i.Insert(0, new SelectListItem { Value = "", Text = "Todos los Dueños" });
            return new SelectList(i, "Value", "Text", selectedDuenoId?.ToString());
        }


        
        [HttpGet]
        public async Task<JsonResult> GetAvailableTimes(string date, int? excludingCitaId = null)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime selectedDate))
            {
                if (!DateTime.TryParse(date, out selectedDate))
                {
                    _logger.LogWarning("GetAvailableTimes: Formato de fecha inválido recibido: {DateString}", date);
                    return Json(new { success = false, message = "Formato de fecha inválido." });
                }
            }

            if (selectedDate.Date <= DateTime.Today)
            {
                return Json(new { success = true, availableTimes = new List<string>() });
            }

            var availableTimes = new List<string>();
            HashSet<DateTime> occupiedSlots;

            try
            {
                var startOfDay = selectedDate.Date;
                var endOfDay = startOfDay.AddDays(1);
                var query = _context.Citas
                    .Where(c => c.FechaHora >= startOfDay && c.FechaHora < endOfDay &&
                                c.Estado != EstadoCita.CanceladaCliente &&
                                c.Estado != EstadoCita.CanceladaStaff);

                if (excludingCitaId.HasValue && excludingCitaId.Value > 0)
                {
                    query = query.Where(c => c.IdCita != excludingCitaId.Value);
                    _logger.LogDebug("GetAvailableTimes: Excluyendo Cita ID {ExcludingId} para fecha {SelectedDate}", excludingCitaId.Value, date);
                }

                var occupiedDateTimes = await query.Select(c => c.FechaHora).ToListAsync();
                occupiedSlots = new HashSet<DateTime>(occupiedDateTimes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetAvailableTimes para {SelectedDate}", date);
                return Json(new { success = false, message = "Error al consultar." });
            }

            var now = DateTime.Now;
            for (int hour = MinAppointmentHour; hour <= MaxAppointmentHour; hour++)
            {
                var potentialSlot = selectedDate.Date.AddHours(hour);
                
                if (potentialSlot > now && !occupiedSlots.Contains(potentialSlot))
                {
                    availableTimes.Add(potentialSlot.ToString("HH:mm"));
                }
            }
            return Json(new { success = true, availableTimes });
        }

        private async Task<bool> CitaExists(int id)
        {
            return await _context.Citas.AnyAsync(e => e.IdCita == id);
        }

    } 
} 