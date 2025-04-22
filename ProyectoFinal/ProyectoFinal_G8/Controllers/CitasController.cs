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
using System.Security.Claims; 

namespace ProyectoFinal_G8.Controllers
{
    [Authorize] // Requiere login para todo
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

        // Helper para obtener el ID del usuario actual de forma segura
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
                // Podrías redirigir a Login o mostrar una vista de error más informativa
                return Unauthorized("No se pudo obtener el ID del usuario.");
            }

            ViewData["CurrentUserID"] = userIdAsInt; // Pasar ID a la vista para comprobaciones

            IQueryable<Cita> citasQuery = _context.Citas
                                                .Include(c => c.Mascota)
                                                    .ThenInclude(m => m.Dueño) // Asegúrate que Dueño es la propiedad de navegación a Usuario
                                                .Include(c => c.Veterinario)
                                                .Include(c => c.TipoCita);

            IList<Cita> citas;


            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Admin/Vet) obteniendo todas las citas.");
                citas = await citasQuery.OrderByDescending(c => c.FechaHora).ToListAsync();
                ViewData["VistaTitulo"] = "Gestión de Citas";
            }
            else if (User.IsInRole("Cliente"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Cliente ID: {userIdAsInt}) obteniendo sus citas.");
                citas = await citasQuery
                              .Where(c => c.Mascota != null && c.Mascota.IdUsuarioDueño == userIdAsInt)
                              .OrderByDescending(c => c.FechaHora)
                              .ToListAsync();
                ViewData["VistaTitulo"] = "Mis Citas";
            }
            else
            {
                _logger.LogWarning($"Usuario {User.Identity?.Name} con rol desconocido intentó acceder a citas.");
                citas = new List<Cita>(); // Lista vacía
                ViewData["VistaTitulo"] = "Citas";
            }

            return View(citas);
        }

        // GET: Citas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) { _logger.LogWarning("Details: ID es null."); return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener el ID del usuario.");

            _logger.LogInformation($"Buscando detalles para cita ID: {id}");
            var cita = await _context.Citas
                .Include(c => c.Mascota).ThenInclude(m => m.Dueño)
                .Include(c => c.Veterinario)
                .Include(c => c.TipoCita)
                .FirstOrDefaultAsync(m => m.IdCita == id);

            if (cita == null) { _logger.LogWarning($"Details: Cita con ID {id} no encontrada."); return NotFound(); }

            // Verificación de Permiso Cliente
            if (User.IsInRole("Cliente") && cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) intentó ver detalles de cita ajena (ID: {id}).");
                TempData["ErrorMessage"] = "No tienes permiso para ver los detalles de esta cita."; // Mensaje más amigable que Forbid
                return RedirectToAction(nameof(Index));
                // return Forbid("No tienes permiso para ver los detalles de esta cita.");
            }

            ViewData["CurrentUserID"] = userIdAsInt; // Pasar ID a la vista
            return View(cita);
        }

        // GET: Citas/Create
        public async Task<IActionResult> Create()
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener el ID del usuario.");

            await LoadVeterinariosAsync();
            await LoadTiposCitaAsync();

            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Admin/Vet) cargando vista Create (todas las mascotas).");
                await LoadTodasMascotasAsync(); // Método auxiliar para cargar todas
            }
            else if (User.IsInRole("Cliente"))
            {
                _logger.LogInformation($"Usuario {User.Identity?.Name} (Cliente ID: {userIdAsInt}) cargando vista Create (sus mascotas).");
                if (!await LoadMisMascotasAsync(userIdAsInt)) // Modificado para devolver bool
                {
                    // LoadMisMascotasAsync ya habrá loggeado y puesto TempData si no hay mascotas
                    return RedirectToAction("Create", "Mascotas");
                }
            }
            else
            {
                _logger.LogWarning($"Usuario {User.Identity?.Name} con rol desconocido intentó acceder a Create GET.");
                return Forbid();
            }

            var nuevaCita = new Cita { FechaHora = DateTime.Now.Date.AddDays(1).AddHours(9) }; // Sugerir mañana a las 9am
            return View(nuevaCita);
        }

        // POST: Citas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Notas")] Cita cita) // Quitamos IdCita del Bind
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener el ID del usuario.");

            // Forzar estado programada ANTES de validar
            cita.Estado = EstadoCita.Programada;

            ModelState.Remove(nameof(Cita.Mascota));
            ModelState.Remove(nameof(Cita.Veterinario));
            ModelState.Remove(nameof(Cita.TipoCita));
            ModelState.Remove(nameof(Cita.Estado)); // No se bindea ni valida

            // Validar Fecha Futura (Usando UtcNow para consistencia)
            if (cita.FechaHora <= DateTime.UtcNow) // <-- Usar UtcNow
            {
                ModelState.AddModelError(nameof(Cita.FechaHora), "La fecha y hora de la cita deben ser en el futuro.");
            }


            if (User.IsInRole("Cliente"))
            {
                // Verificar pertenencia de mascota para cliente
                bool mascotaValida = await _context.Mascotas
                                                .AnyAsync(m => m.IdMascota == cita.IdMascota && m.IdUsuarioDueño == userIdAsInt);
                if (!mascotaValida)
                {
                    _logger.LogWarning($"Create POST: Cliente {userIdAsInt} intentó crear cita con mascota inválida/ajena (ID Mascota: {cita.IdMascota}).");
                    ModelState.AddModelError(nameof(Cita.IdMascota), "La mascota seleccionada no es válida o no te pertenece.");
                }
            }
            // No necesitamos else para Admin/Vet aquí, ya que pueden seleccionar cualquiera

            if (ModelState.IsValid)
            {
                _logger.LogInformation($"Intentando guardar nueva cita para Mascota ID: {cita.IdMascota} por Usuario: {User.Identity?.Name}");
                _context.Add(cita);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Cita ID: {cita.IdCita} guardada exitosamente con estado '{cita.Estado}'.");
                    TempData["SuccessMessage"] = User.IsInRole("Cliente") ? "Tu cita ha sido programada correctamente." : "Cita creada exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException dbEx) // Error específico BD
                {
                    _logger.LogError(dbEx, $"Error BD guardando nueva cita para Mascota ID: {cita.IdMascota}. InnerEx: {dbEx.InnerException?.Message}");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar en la base de datos. Verifique los datos seleccionados.");
                }
                catch (Exception ex) // Error general
                {
                    _logger.LogError(ex, $"Error general guardando nueva cita para Mascota ID: {cita.IdMascota}");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al guardar la cita.");
                }
            }
            else
            {
                _logger.LogWarning($"Create POST: ModelState inválido. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            }

            // Si falla validación o guardado, recargar listas según rol
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            { await LoadTodasMascotasAsync(cita.IdMascota); }
            else
            { await LoadMisMascotasAsync(userIdAsInt, cita.IdMascota); }

            return View(cita);
        }


        // GET: Citas/Edit/5 (Solo Admin/Veterinario)
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            _logger.LogInformation($"Admin/Vet {User.Identity?.Name} cargando vista Edit para Cita ID: {id}");

            var cita = await _context.Citas.FindAsync(id);
            if (cita == null) { _logger.LogWarning($"Edit GET (Admin/Vet): Cita ID {id} no encontrada."); return NotFound(); }

            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            await LoadTodasMascotasAsync(cita.IdMascota);
            await LoadEstadosCitaAsync(cita.Estado);

            return View(cita);
        }

        // POST: Citas/Edit/5 (Solo Admin/Veterinario)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Edit(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Estado,Notas")] Cita citaViewModel)
        {
            if (id != citaViewModel.IdCita) { _logger.LogWarning($"Edit POST (Admin/Vet): ID de ruta ({id}) no coincide con ID de modelo ({citaViewModel.IdCita})."); return BadRequest("ID de cita no coincide."); }

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
                    _logger.LogInformation($"Admin/Vet {User.Identity?.Name} intentando actualizar Cita ID: {id}");
                    _context.Update(citaViewModel);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Cita ID: {id} actualizada por Admin/Vet.");
                    TempData["SuccessMessage"] = "Cita actualizada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, $"Error de concurrencia actualizando Cita ID: {id}");
                    if (!await CitaExists(citaViewModel.IdCita))
                    {
                        _logger.LogWarning($"Edit POST (Admin/Vet): Cita ID {id} no encontrada durante concurrencia.");
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Los datos de esta cita fueron modificados por otro usuario. Recargue la página e intente de nuevo.");
                    }
                }
                catch (DbUpdateException dbEx) // Error específico BD
                {
                    _logger.LogError(dbEx, $"Error BD actualizando Cita ID: {id} por Admin/Vet. InnerEx: {dbEx.InnerException?.Message}");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar en la base de datos. Verifique los datos seleccionados.");
                }
                catch (Exception ex) // Error general
                {
                    _logger.LogError(ex, $"Error general actualizando Cita ID: {id} por Admin/Vet.");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al actualizar la cita.");
                }
            }
            else
            {
                _logger.LogWarning($"Edit POST (Admin/Vet): ModelState inválido para Cita ID: {id}. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            }

            await LoadVeterinariosAsync(citaViewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(citaViewModel.IdTipoCita);
            await LoadTodasMascotasAsync(citaViewModel.IdMascota);
            await LoadEstadosCitaAsync(citaViewModel.Estado);
            return View(citaViewModel);
        }

        // --- Edición para Cliente ---

        // GET: Citas/EditCliente/5
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> EditCliente(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            _logger.LogInformation($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) cargando vista EditCliente para Cita ID: {id}");

            var cita = await _context.Citas
                             .Include(c => c.Mascota) // Incluir Mascota para verificar dueño
                             .FirstOrDefaultAsync(c => c.IdCita == id);

            if (cita == null) { _logger.LogWarning($"EditCliente GET: Cita ID {id} no encontrada."); return NotFound(); }

            // *** INICIO: Verificaciones de Seguridad y Lógica de Negocio para Cliente ***
            string? errorMessage = null;

            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"EditCliente GET: Cliente {userIdAsInt} intentó editar cita ajena {id}.");
                errorMessage = "No tienes permiso para editar esta cita.";
            }
            else if (cita.Estado != EstadoCita.Programada)
            {
                _logger.LogWarning($"EditCliente GET: Cliente {userIdAsInt} intentó editar cita {id} con estado '{cita.Estado}'. Se compara con '{EstadoCita.Programada}'");
                errorMessage = "La cita ya no puede ser modificada porque su estado no es 'Programada'.";
            }
            // --- Comprobación de Fecha (Usando UtcNow) ---
            else if (cita.FechaHora <= DateTime.UtcNow) // <-- Usando UTC
                                                        // --- Fin Comprobación de Fecha ---
            {
                _logger.LogWarning($"EditCliente GET: Cliente {userIdAsInt} intentó editar cita pasada/presente {id}. Cita: {cita.FechaHora}, Comparada con UTC: {DateTime.UtcNow}");
                errorMessage = "La cita ya no puede ser modificada porque la fecha ya pasó o es ahora mismo.";
            }


            if (errorMessage != null)
            {
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToAction(nameof(Index));
            }
            // *** FIN: Verificaciones ***

            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            await LoadMisMascotasAsync(userIdAsInt, cita.IdMascota);

            return View("EditCliente", cita);
        }

        // POST: Citas/EditCliente/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> EditCliente(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Notas")] Cita citaViewModel)
        {
            if (id != citaViewModel.IdCita) { return BadRequest("ID de cita no coincide."); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            var citaOriginal = await _context.Citas
                                     .Include(c => c.Mascota)
                                     .AsNoTracking()
                                     .FirstOrDefaultAsync(c => c.IdCita == id);

            if (citaOriginal == null) { _logger.LogWarning($"EditCliente POST: Cita Original ID {id} no encontrada."); return NotFound(); }

            // *** Repetir Verificaciones contra la cita ORIGINAL ***
            string? redirectErrorMessage = null;
            if (citaOriginal.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                redirectErrorMessage = "No tienes permiso para editar esta cita.";
            }
            else if (citaOriginal.Estado != EstadoCita.Programada)
            {
                redirectErrorMessage = "La cita ya no puede ser modificada porque su estado cambió mientras editabas.";
            }
            // --- Comprobación de Fecha Original (Usando UtcNow) ---
            else if (citaOriginal.FechaHora <= DateTime.UtcNow) // <-- Usando UTC
                                                                // --- Fin Comprobación ---
            {
                redirectErrorMessage = "La cita ya no puede ser modificada porque su fecha original ya pasó.";
            }

            if (redirectErrorMessage != null)
            {
                _logger.LogWarning($"EditCliente POST: Redirigiendo para cita {id} por validación fallida contra original: {redirectErrorMessage}");
                TempData["ErrorMessage"] = redirectErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            // *** Validaciones del ViewModel ***
            ModelState.Remove(nameof(Cita.Mascota));
            ModelState.Remove(nameof(Cita.Veterinario));
            ModelState.Remove(nameof(Cita.TipoCita));
            ModelState.Remove(nameof(Cita.Estado));

            // Validar Fecha Futura para el nuevo valor (Usando UtcNow)
            // --- Comprobación de Nueva Fecha ---
            if (citaViewModel.FechaHora <= DateTime.UtcNow) // <-- Usando UTC
                                                            // --- Fin Comprobación ---
            {
                ModelState.AddModelError(nameof(Cita.FechaHora), "La nueva fecha y hora de la cita deben ser en el futuro.");
            }

            bool mascotaSeleccionadaValida = await _context.Mascotas
                                                     .AnyAsync(m => m.IdMascota == citaViewModel.IdMascota && m.IdUsuarioDueño == userIdAsInt);
            if (!mascotaSeleccionadaValida)
            {
                ModelState.AddModelError(nameof(Cita.IdMascota), "La mascota seleccionada no es válida.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) intentando actualizar Cita ID: {id}");

                    // Cargar la entidad para actualizarla (Sin AsNoTracking)
                    // Incluir Mascota es importante para la verificación de dueño
                    var citaParaActualizar = await _context.Citas
                                                   .Include(c => c.Mascota) // Asegurar que Mascota se carga
                                                   .FirstOrDefaultAsync(c => c.IdCita == id);

                    if (citaParaActualizar == null) return NotFound();

                    // Re-verificar estado y dueño (concurrencia leve + seguridad)
                    // *** ¡¡¡ LÍNEA CORREGIDA AQUÍ !!! ***
                    if (citaParaActualizar.Mascota?.IdUsuarioDueño != userIdAsInt || citaParaActualizar.Estado != EstadoCita.Programada)
                    {
                        TempData["ErrorMessage"] = "La cita ya no cumple las condiciones para ser modificada. Inténtalo de nuevo.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Actualizar campos permitidos
                    citaParaActualizar.FechaHora = citaViewModel.FechaHora;
                    citaParaActualizar.IdMascota = citaViewModel.IdMascota;
                    citaParaActualizar.IdUsuarioVeterinario = citaViewModel.IdUsuarioVeterinario;
                    citaParaActualizar.IdTipoCita = citaViewModel.IdTipoCita;
                    citaParaActualizar.Notas = citaViewModel.Notas;
                    // El Estado NO SE CAMBIA

                    await _context.SaveChangesAsync(); // Guardar los cambios rastreados

                    _logger.LogInformation($"Cita ID: {id} actualizada por Cliente {userIdAsInt}.");
                    TempData["SuccessMessage"] = "Tu cita ha sido modificada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, $"Error de concurrencia actualizando Cita ID: {id} por cliente {userIdAsInt}");
                    ModelState.AddModelError(string.Empty, "Hubo un conflicto al guardar los cambios (posiblemente alguien más modificó la cita). Por favor, recarga e intenta de nuevo.");
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, $"Error de BD actualizando Cita ID: {id} por cliente {userIdAsInt}. InnerEx: {dbEx.InnerException?.Message}");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar los cambios en la base de datos.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error general actualizando Cita ID: {id} por cliente {userIdAsInt}.");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error inesperado al modificar la cita.");
                }
            }
            else
            {
                _logger.LogWarning($"EditCliente POST: ModelState inválido para Cita ID: {id} por cliente {userIdAsInt}. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            }

            await LoadVeterinariosAsync(citaViewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(citaViewModel.IdTipoCita);
            await LoadMisMascotasAsync(userIdAsInt, citaViewModel.IdMascota);
            return View("EditCliente", citaViewModel);
        }


        // --- Cancelación por Cliente ---

        // GET: Citas/CancelCliente/5
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CancelCliente(int? id)
        {
            if (id == null) { return NotFound(); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            _logger.LogInformation($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) cargando vista CancelCliente para Cita ID: {id}");

            var cita = await _context.Citas
                             .Include(c => c.Mascota)
                             .Include(c => c.Veterinario)
                             .Include(c => c.TipoCita)
                             .FirstOrDefaultAsync(c => c.IdCita == id);

            if (cita == null) { _logger.LogWarning($"CancelCliente GET: Cita ID {id} no encontrada."); return NotFound(); }

            // *** Verificaciones de Seguridad y Lógica ***
            string? errorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                errorMessage = "No tienes permiso para cancelar esta cita.";
            }
            else if (cita.Estado != EstadoCita.Programada)
            {
                errorMessage = "Solo puedes cancelar citas que estén 'Programada'.";
            }
            // --- Comprobación de Fecha (Usando UtcNow) ---
            else if (cita.FechaHora <= DateTime.UtcNow) // <-- Usando UTC
                                                        // --- Fin Comprobación ---
            {
                errorMessage = "No puedes cancelar una cita que ya ha pasado.";
            }

            if (errorMessage != null)
            {
                _logger.LogWarning($"CancelCliente GET: Redirigiendo para cita {id} por validación fallida: {errorMessage}");
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
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            var cita = await _context.Citas.Include(c => c.Mascota).FirstOrDefaultAsync(c => c.IdCita == id);

            if (cita == null) { _logger.LogWarning($"CancelCliente POST: Cita ID {id} no encontrada para confirmar cancelación."); return NotFound(); }

            // *** Repetir Verificaciones ***
            string? redirectErrorMessage = null;
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                redirectErrorMessage = "No tienes permiso para cancelar esta cita.";
            }
            else if (cita.Estado != EstadoCita.Programada)
            {
                redirectErrorMessage = "Esta cita ya no se puede cancelar porque su estado ha cambiado.";
            }
            // --- Comprobación de Fecha (Usando UtcNow) ---
            else if (cita.FechaHora <= DateTime.UtcNow) // <-- Usando UTC
                                                        // --- Fin Comprobación ---
            {
                redirectErrorMessage = "Esta cita ya no se puede cancelar porque la fecha ya pasó.";
            }

            if (redirectErrorMessage != null)
            {
                _logger.LogWarning($"CancelCliente POST: Redirigiendo para cita {id} por validación fallida contra original: {redirectErrorMessage}");
                TempData["ErrorMessage"] = redirectErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _logger.LogWarning($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) confirmando cancelación para Cita ID: {id}");
                cita.Estado = EstadoCita.CanceladaCliente;
                await _context.SaveChangesAsync();
                _logger.LogWarning($"Cita ID: {id} cancelada por Cliente {userIdAsInt}.");
                TempData["SuccessMessage"] = "Tu cita ha sido cancelada correctamente.";
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, $"Error de concurrencia cancelando Cita ID: {id} por cliente {userIdAsInt}");
                TempData["ErrorMessage"] = "Ocurrió un conflicto al intentar cancelar la cita. Por favor, revisa el estado actual de la cita.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelando Cita ID: {id} por cliente {userIdAsInt}");
                TempData["ErrorMessage"] = "Ocurrió un error inesperado al cancelar la cita.";
            }

            return RedirectToAction(nameof(Index));
        }


        // --- Borrado (Solo Admin/Veterinario) ---

        // GET: Citas/Delete/5
        [Authorize(Roles = "Admin, Veterinario")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            _logger.LogInformation($"Admin/Vet {User.Identity?.Name} cargando vista Delete para Cita ID: {id}");

            var cita = await _context.Citas
                .Include(c => c.Mascota).ThenInclude(m => m.Dueño)
                .Include(c => c.Veterinario)
                .Include(c => c.TipoCita)
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
            _logger.LogWarning($"Admin/Vet {User.Identity?.Name} intentando eliminar Cita ID: {id}");
            var cita = await _context.Citas.FindAsync(id);

            if (cita != null)
            {
                try
                {
                    _context.Citas.Remove(cita);
                    await _context.SaveChangesAsync();
                    _logger.LogWarning($"Cita ID: {id} eliminada permanentemente por {User.Identity?.Name}.");
                    TempData["SuccessMessage"] = "Cita eliminada permanentemente.";
                }
                catch (DbUpdateException dbEx)
                {
                    _logger.LogError(dbEx, $"Error de BD eliminando Cita ID: {id} por {User.Identity?.Name}. InnerEx: {dbEx.InnerException?.Message}");
                    TempData["ErrorMessage"] = "No se pudo eliminar la cita. Puede que tenga datos relacionados que impiden su borrado (ej: historial, facturas).";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error eliminando Cita ID: {id} por {User.Identity?.Name}");
                    TempData["ErrorMessage"] = "Ocurrió un error inesperado al eliminar la cita.";
                    return RedirectToAction(nameof(Delete), new { id = id });
                }
            }
            else
            {
                _logger.LogWarning($"Delete POST: Cita ID {id} no encontrada para eliminar.");
                TempData["ErrorMessage"] = "La cita que intentaba eliminar ya no existe.";
            }
            return RedirectToAction(nameof(Index));
        }

        // --- Métodos Auxiliares ---

        private async Task LoadVeterinariosAsync(object? selectedVeterinario = null)
        {
            var veterinarios = await _userManager.GetUsersInRoleAsync("Veterinario");
            var veterinarioList = veterinarios.Select(u => new {
                Id = u.Id,
                NombreCompleto = u.Nombre // Mantenido como estaba en tu código
            }).OrderBy(u => u.NombreCompleto).ToList();

            ViewData["IdUsuarioVeterinario"] = new SelectList(veterinarioList, "Id", "NombreCompleto", selectedVeterinario);
            _logger.LogDebug($"Cargados {veterinarioList.Count} veterinarios para dropdown.");
        }

        private async Task<bool> LoadMisMascotasAsync(int currentUserId, object? selectedMascota = null)
        {
            var misMascotas = await _context.Mascotas
                                          .Where(m => m.IdUsuarioDueño == currentUserId)
                                          .OrderBy(m => m.Nombre)
                                          .Select(m => new { m.IdMascota, m.Nombre }) // Seleccionar solo lo necesario
                                          .ToListAsync();

            if (!misMascotas.Any())
            {
                _logger.LogWarning($"LoadMisMascotasAsync: Cliente ID {currentUserId} no tiene mascotas registradas.");
                TempData["ErrorMessage"] = "No tienes mascotas registradas. Debes registrar una mascota antes de poder solicitar una cita.";
                ViewData["IdMascota"] = new SelectList(Enumerable.Empty<SelectListItem>(), "IdMascota", "Nombre");
                return false;
            }

            ViewData["IdMascota"] = new SelectList(misMascotas, "IdMascota", "Nombre", selectedMascota);
            _logger.LogDebug($"LoadMisMascotasAsync: Cargadas {misMascotas.Count} mascotas para el cliente ID: {currentUserId}");
            return true;
        }

        private async Task LoadTodasMascotasAsync(object? selectedMascota = null)
        {
            var todasMascotas = await _context.Mascotas
                                            .Include(m => m.Dueño)
                                            .OrderBy(m => m.Nombre)
                                            .ToListAsync();

            var mascotaSelectListItems = todasMascotas.Select(m => new SelectListItem
            {
                Value = m.IdMascota.ToString(),
                Text = $"{m.Nombre} (Dueño: {m.Dueño?.Nombre ?? "N/A"})" // Mantenido como estaba en tu código
            }).ToList();

            ViewData["IdMascota"] = new SelectList(mascotaSelectListItems, "Value", "Text", selectedMascota);
            _logger.LogDebug($"LoadTodasMascotasAsync: Cargadas {todasMascotas.Count} mascotas totales para dropdown (Admin/Vet).");
        }

        private async Task LoadTiposCitaAsync(object? selectedTipo = null)
        {
            _logger.LogDebug("Cargando tipos de cita para dropdown.");
            var tipos = await _context.TiposCita
                                   .OrderBy(t => t.Nombre)
                                   .Select(t => new { t.IdTipoCita, t.Nombre })
                                   .ToListAsync();
            ViewData["IdTipoCita"] = new SelectList(tipos, "IdTipoCita", "Nombre", selectedTipo);
        }

        private Task LoadEstadosCitaAsync(object? selectedEstado = null)
        {
            _logger.LogDebug("Cargando estados de cita para dropdown (Admin/Vet).");
            var estadosDisponibles = EstadoCita.GetEstadosEditables()
                                              .Select(e => new SelectListItem { Value = e, Text = e })
                                              .ToList();
            ViewData["EstadosCita"] = new SelectList(estadosDisponibles, "Value", "Text", selectedEstado);
            return Task.CompletedTask;
        }

        private async Task<bool> CitaExists(int id)
        {
            return await _context.Citas.AnyAsync(e => e.IdCita == id);
        }
    }
}