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
using System.Security.Claims; // Necesario para User

namespace ProyectoFinal_G8.Controllers
{
    [Authorize] // Requiere login para todo
    public class CitasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<CitasController> _logger;

        // Definir constantes para estados para evitar errores de escritura
        // private const string ESTADO_PROGRAMADA = "Programada"; // Usar los de la clase estática
        // private const string ESTADO_CANCELADA_CLIENTE = "Cancelada por Cliente";

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
                return Unauthorized("No se pudo obtener el ID del usuario.");
            }

            IQueryable<Cita> citasQuery = _context.Citas
                                                .Include(c => c.Mascota)
                                                    .ThenInclude(m => m.Dueño) // Asegúrate que Dueño es la propiedad de navegación a Usuario
                                                .Include(c => c.Veterinario)
                                                .Include(c => c.TipoCita);

            IList<Cita> citas;
            ViewData["CurrentUserID"] = userIdAsInt; // Pasar ID a la vista para comprobaciones

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
                return Forbid("No tienes permiso para ver los detalles de esta cita.");
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
        // Bind actualizado: Quitado Estado explícitamente, se manejará en código
        public async Task<IActionResult> Create([Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Notas")] Cita cita)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) return Unauthorized("No se pudo obtener el ID del usuario.");

            // Remover navegaciones para evitar problemas de validación automática
            ModelState.Remove("Mascota");
            ModelState.Remove("Veterinario");
            ModelState.Remove("TipoCita");
            ModelState.Remove("Estado"); // Remover Estado de la validación explícita si no se bindea

            // Validar Fecha Futura (si no se hizo con atributo)
            if (cita.FechaHora <= DateTime.Now)
            {
                ModelState.AddModelError("FechaHora", "La fecha y hora de la cita deben ser en el futuro.");
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
                }
                // Forzar estado para cliente
                cita.Estado = EstadoCita.Programada;
            }
            else if (User.IsInRole("Admin") || User.IsInRole("Veterinario"))
            {
                // Permitir que Admin/Vet establezcan estado inicial o usar uno por defecto si no lo envían
                // Como no está en Bind, necesitamos asignarlo manualmente si quisiéramos permitirlo,
                // o simplemente usar el default del modelo/constructor.
                // Por simplicidad, dejaremos que tome el default "Programada" o lo cambien en Edit.
                cita.Estado ??= EstadoCita.Programada; // Asegura que no sea null
            }
            else
            {
                return Forbid(); // Rol no autorizado
            }


            if (ModelState.IsValid)
            {
                _logger.LogInformation($"Intentando guardar nueva cita para Mascota ID: {cita.IdMascota} por Usuario: {User.Identity?.Name}");
                _context.Add(cita);
                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Cita ID: {cita.IdCita} guardada exitosamente con estado '{cita.Estado}'.");
                    TempData["SuccessMessage"] = User.IsInRole("Cliente") ? "Su cita ha sido programada." : "Cita creada exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error guardando nueva cita para Mascota ID: {cita.IdMascota}");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar la cita. Verifique los datos e intente de nuevo.");
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

            // Admin/Vet pueden editar citas pasadas (para marcar como Realizada/No asistió)
            // No hay chequeo de fecha aquí

            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            await LoadTodasMascotasAsync(cita.IdMascota); // Admin/Vet pueden cambiar a cualquier mascota
            await LoadEstadosCitaAsync(cita.Estado); // Cargar estados para el dropdown

            return View(cita);
        }

        // POST: Citas/Edit/5 (Solo Admin/Veterinario)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Veterinario")]
        // Bind incluye Estado para Admin/Vet
        public async Task<IActionResult> Edit(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Estado,Notas")] Cita cita)
        {
            if (id != cita.IdCita) { _logger.LogWarning($"Edit POST (Admin/Vet): ID de ruta ({id}) no coincide con ID de modelo ({cita.IdCita})."); return BadRequest("ID de cita no coincide."); }

            ModelState.Remove("Mascota");
            ModelState.Remove("Veterinario");
            ModelState.Remove("TipoCita");

            // Validar que el estado seleccionado sea uno válido (si se usa dropdown)
            if (!EstadoCita.GetEstadosEditables().Contains(cita.Estado ?? ""))
            {
                ModelState.AddModelError("Estado", "El estado seleccionado no es válido.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation($"Admin/Vet {User.Identity?.Name} intentando actualizar Cita ID: {id}");
                    _context.Update(cita);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Cita ID: {id} actualizada por Admin/Vet.");
                    TempData["SuccessMessage"] = "Cita actualizada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, $"Error de concurrencia actualizando Cita ID: {id}");
                    if (!await CitaExists(cita.IdCita)) { return NotFound(); }
                    else { ModelState.AddModelError(string.Empty, "Los datos fueron modificados por otro usuario. Recargue e intente de nuevo."); }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error general actualizando Cita ID: {id} por Admin/Vet.");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar la cita.");
                }
            }
            else
            {
                _logger.LogWarning($"Edit POST (Admin/Vet): ModelState inválido para Cita ID: {id}. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            }

            // Si llegamos aquí, algo falló, recargar datos y mostrar vista
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            await LoadTodasMascotasAsync(cita.IdMascota);
            await LoadEstadosCitaAsync(cita.Estado);
            return View(cita);
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
                             .Include(c => c.Mascota) // Necesario para verificar dueño
                             .FirstOrDefaultAsync(c => c.IdCita == id);

            if (cita == null) { _logger.LogWarning($"EditCliente GET: Cita ID {id} no encontrada."); return NotFound(); }

            // *** Verificaciones de Seguridad y Lógica de Negocio para Cliente ***
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"EditCliente GET: Cliente {userIdAsInt} intentó editar cita ajena {id}.");
                return Forbid("No tienes permiso para editar esta cita.");
            }
            if (cita.Estado != EstadoCita.Programada)
            {
                _logger.LogWarning($"EditCliente GET: Cliente {userIdAsInt} intentó editar cita {id} con estado '{cita.Estado}'.");
                TempData["ErrorMessage"] = "Solo puedes modificar citas que estén 'Programada'.";
                return RedirectToAction(nameof(Index));
            }
            if (cita.FechaHora <= DateTime.Now)
            {
                _logger.LogWarning($"EditCliente GET: Cliente {userIdAsInt} intentó editar cita pasada {id}.");
                TempData["ErrorMessage"] = "No puedes modificar una cita que ya ha pasado.";
                return RedirectToAction(nameof(Index));
            }
            // Opcional: No permitir editar si falta poco tiempo (e.g., menos de 24 horas)
            // if (cita.FechaHora <= DateTime.Now.AddHours(24)) { ... }


            // Cargar datos para los dropdowns
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(cita.IdTipoCita);
            await LoadMisMascotasAsync(userIdAsInt, cita.IdMascota); // Solo las mascotas del cliente

            // Pasar la cita a una vista específica para cliente
            return View("EditCliente", cita);
        }

        // POST: Citas/EditCliente/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cliente")]
        // Bind SIN Estado - Cliente no puede cambiar estado aquí
        public async Task<IActionResult> EditCliente(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,IdTipoCita,Notas")] Cita citaViewModel)
        {
            if (id != citaViewModel.IdCita) { return BadRequest("ID de cita no coincide."); }
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            // *** Obtener la Cita ORIGINAL de la BD para verificar permisos ANTES de validar modelo ***
            var citaOriginal = await _context.Citas
                                     .Include(c => c.Mascota)
                                     .AsNoTracking() // Importante para poder actualizarla luego sin conflictos
                                     .FirstOrDefaultAsync(c => c.IdCita == id);

            if (citaOriginal == null) { return NotFound(); }

            // *** Repetir Verificaciones de Seguridad y Lógica ***
            if (citaOriginal.Mascota?.IdUsuarioDueño != userIdAsInt) { return Forbid("No tienes permiso para editar esta cita."); }
            if (citaOriginal.Estado != EstadoCita.Programada)
            {
                TempData["ErrorMessage"] = "Esta cita ya no se puede modificar porque su estado ha cambiado.";
                return RedirectToAction(nameof(Index));
            }
            if (citaOriginal.FechaHora <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Esta cita ya no se puede modificar porque la fecha ya pasó.";
                return RedirectToAction(nameof(Index));
            }
            // Validar Fecha Futura para el nuevo valor
            if (citaViewModel.FechaHora <= DateTime.Now)
            {
                ModelState.AddModelError("FechaHora", "La nueva fecha y hora de la cita deben ser en el futuro.");
            }
            // Validar que la mascota seleccionada pertenece al usuario (aunque el dropdown ya debería filtrarlo)
            var mascotaSeleccionadaValida = await _context.Mascotas
                                                     .AnyAsync(m => m.IdMascota == citaViewModel.IdMascota && m.IdUsuarioDueño == userIdAsInt);
            if (!mascotaSeleccionadaValida)
            {
                ModelState.AddModelError("IdMascota", "La mascota seleccionada no es válida.");
            }


            // Remover navegaciones del ModelState si causa problemas
            ModelState.Remove("Mascota");
            ModelState.Remove("Veterinario");
            ModelState.Remove("TipoCita");
            ModelState.Remove("Estado");


            if (ModelState.IsValid)
            {
                try
                {
                    _logger.LogInformation($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) intentando actualizar Cita ID: {id}");

                    // Actualizar la entidad original SOLO con los campos permitidos
                    // Hay que volver a cargarla si usamos AsNoTracking antes, o अटैच y modificar estado
                    var citaParaActualizar = await _context.Citas.FindAsync(id);
                    if (citaParaActualizar == null) return NotFound(); // Doble check por si se borró

                    // Re-verificar estado y dueño por si cambió entre el GET y POST (concurrencia)
                    if (citaParaActualizar.Mascota?.IdUsuarioDueño != userIdAsInt || citaParaActualizar.Estado != EstadoCita.Programada || citaParaActualizar.FechaHora <= DateTime.Now)
                    {
                        TempData["ErrorMessage"] = "La cita ya no puede ser modificada.";
                        return RedirectToAction(nameof(Index));
                    }

                    citaParaActualizar.FechaHora = citaViewModel.FechaHora;
                    citaParaActualizar.IdMascota = citaViewModel.IdMascota;
                    citaParaActualizar.IdUsuarioVeterinario = citaViewModel.IdUsuarioVeterinario;
                    citaParaActualizar.IdTipoCita = citaViewModel.IdTipoCita;
                    citaParaActualizar.Notas = citaViewModel.Notas;
                    // El Estado NO SE CAMBIA aquí, permanece "Programada"

                    _context.Update(citaParaActualizar);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Cita ID: {id} actualizada por Cliente {userIdAsInt}.");
                    TempData["SuccessMessage"] = "Tu cita ha sido modificada correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, $"Error de concurrencia actualizando Cita ID: {id} por cliente {userIdAsInt}");
                    if (!await CitaExists(citaViewModel.IdCita)) { return NotFound(); }
                    else { ModelState.AddModelError(string.Empty, "Hubo un problema al guardar los cambios. Intente de nuevo."); }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error general actualizando Cita ID: {id} por cliente {userIdAsInt}.");
                    ModelState.AddModelError(string.Empty, "Ocurrió un error al modificar la cita.");
                }
            }
            else
            {
                _logger.LogWarning($"EditCliente POST: ModelState inválido para Cita ID: {id} por cliente {userIdAsInt}. Errores: {string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
            }

            // Si falla validación o guardado, recargar datos y mostrar vista EditCliente
            await LoadVeterinariosAsync(citaViewModel.IdUsuarioVeterinario);
            await LoadTiposCitaAsync(citaViewModel.IdTipoCita);
            await LoadMisMascotasAsync(userIdAsInt, citaViewModel.IdMascota);
            return View("EditCliente", citaViewModel); // Devolver el modelo con errores
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
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"CancelCliente GET: Cliente {userIdAsInt} intentó cancelar cita ajena {id}.");
                return Forbid("No tienes permiso para cancelar esta cita.");
            }
            if (cita.Estado != EstadoCita.Programada)
            {
                _logger.LogWarning($"CancelCliente GET: Cliente {userIdAsInt} intentó cancelar cita {id} con estado '{cita.Estado}'.");
                TempData["ErrorMessage"] = "Solo puedes cancelar citas que estén 'Programada'.";
                return RedirectToAction(nameof(Index));
            }
            if (cita.FechaHora <= DateTime.Now)
            {
                _logger.LogWarning($"CancelCliente GET: Cliente {userIdAsInt} intentó cancelar cita pasada {id}.");
                TempData["ErrorMessage"] = "No puedes cancelar una cita que ya ha pasado.";
                return RedirectToAction(nameof(Index));
            }
            // Opcional: No permitir cancelar si falta poco tiempo (e.g., menos de 24 horas)
            // if (cita.FechaHora <= DateTime.Now.AddHours(24)) { ... }

            // Pasar a la vista de confirmación
            return View("CancelCliente", cita);
        }

        // POST: Citas/CancelCliente/5 (Confirmación)
        [HttpPost, ActionName("CancelCliente")] // Ojo al ActionName si la vista apunta a CancelCliente
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CancelClienteConfirmed(int id)
        {
            if (!TryGetCurrentUserId(out int userIdAsInt)) { return Unauthorized("No se pudo obtener el ID del usuario."); }

            var cita = await _context.Citas.Include(c => c.Mascota).FirstOrDefaultAsync(c => c.IdCita == id); // Incluir Mascota para verificar dueño

            if (cita == null) { _logger.LogWarning($"CancelCliente POST: Cita ID {id} no encontrada."); return NotFound(); }

            // *** Repetir Verificaciones de Seguridad y Lógica ***
            if (cita.Mascota?.IdUsuarioDueño != userIdAsInt)
            {
                _logger.LogWarning($"CancelCliente POST: Cliente {userIdAsInt} intentó confirmar cancelación de cita ajena {id}.");
                return Forbid("No tienes permiso para cancelar esta cita.");
            }
            if (cita.Estado != EstadoCita.Programada)
            {
                _logger.LogWarning($"CancelCliente POST: Cliente {userIdAsInt} intentó confirmar cancelación de cita {id} con estado '{cita.Estado}'.");
                TempData["ErrorMessage"] = "Esta cita ya no se puede cancelar porque su estado ha cambiado.";
                return RedirectToAction(nameof(Index));
            }
            if (cita.FechaHora <= DateTime.Now)
            {
                _logger.LogWarning($"CancelCliente POST: Cliente {userIdAsInt} intentó confirmar cancelación de cita pasada {id}.");
                TempData["ErrorMessage"] = "Esta cita ya no se puede cancelar porque la fecha ya pasó.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _logger.LogWarning($"Cliente {User.Identity?.Name} (ID: {userIdAsInt}) confirmando cancelación para Cita ID: {id}");
                cita.Estado = EstadoCita.CanceladaCliente; // Cambiar estado
                _context.Update(cita);
                await _context.SaveChangesAsync();
                _logger.LogWarning($"Cita ID: {id} cancelada por Cliente {userIdAsInt}.");
                TempData["SuccessMessage"] = "Tu cita ha sido cancelada.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelando Cita ID: {id} por cliente {userIdAsInt}");
                TempData["ErrorMessage"] = "Ocurrió un error al cancelar la cita.";
                // Podríamos redirigir a Details o Index, Index es más común
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
                .Include(c => c.Mascota).ThenInclude(m => m.Dueño) // Incluir Dueño para mostrar
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error eliminando Cita ID: {id} por {User.Identity?.Name}");
                    TempData["ErrorMessage"] = "Ocurrió un error al eliminar la cita. Verifique si tiene datos relacionados.";
                    // Podría haber restricciones de FK si hay otros datos ligados a la cita
                    return RedirectToAction(nameof(Delete), new { id = id }); // Volver a la vista de borrado con error
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
            // Asumiendo que tu modelo Usuario tiene Nombre y Apellido
            var veterinarioList = veterinarios.Select(u => new {
                Id = u.Id, // Asegúrate que el Id es int si la FK es int
                NombreCompleto = u.Nombre // Ajusta según las propiedades de tu clase Usuario
            }).OrderBy(u => u.NombreCompleto).ToList();

            ViewData["IdUsuarioVeterinario"] = new SelectList(veterinarioList, "Id", "NombreCompleto", selectedVeterinario);
            _logger.LogDebug($"Cargados {veterinarioList.Count} veterinarios para dropdown.");
        }

        // Carga SOLO las mascotas del cliente actual
        private async Task<bool> LoadMisMascotasAsync(int currentUserId, object? selectedMascota = null)
        {
            var misMascotas = await _context.Mascotas
                                          .Where(m => m.IdUsuarioDueño == currentUserId)
                                          .OrderBy(m => m.Nombre)
                                          .ToListAsync();

            if (!misMascotas.Any())
            {
                _logger.LogWarning($"Cliente ID: {currentUserId} no tiene mascotas registradas.");
                TempData["ErrorMessage"] = "No tienes mascotas registradas. Debes registrar una mascota antes de poder solicitar una cita.";
                ViewData["IdMascota"] = new SelectList(new List<Mascota>(), "IdMascota", "Nombre"); // SelectList vacío
                return false; // Indica que no se encontraron mascotas
            }

            ViewData["IdMascota"] = new SelectList(misMascotas, "IdMascota", "Nombre", selectedMascota);
            _logger.LogDebug($"Cargadas {misMascotas.Count} mascotas para el cliente ID: {currentUserId}");
            return true; // Indica que se encontraron mascotas
        }

        // Carga TODAS las mascotas (para Admin/Vet)
        private async Task LoadTodasMascotasAsync(object? selectedMascota = null)
        {
            var todasMascotas = await _context.Mascotas
                                            .Include(m => m.Dueño) // Incluir dueño para mostrar nombre completo
                                            .OrderBy(m => m.Nombre)
                                            .ToListAsync();
            // Crear texto descriptivo: "NombreMascota (Dueño: NombreDueño)"
            var mascotaSelectList = todasMascotas.Select(m => new SelectListItem
            {
                Value = m.IdMascota.ToString(),
                Text = $"{m.Nombre} (Dueño: {m.Dueño?.Nombre ?? "N/A"})", // Ajusta 'Nombre' según tu modelo Usuario
                Selected = m.IdMascota.Equals(selectedMascota)
            }).ToList();


            ViewData["IdMascota"] = new SelectList(mascotaSelectList, "Value", "Text", selectedMascota);
            _logger.LogDebug($"Cargadas {todasMascotas.Count} mascotas totales para dropdown (Admin/Vet).");
        }


        private async Task LoadTiposCitaAsync(object? selectedTipo = null)
        {
            _logger.LogDebug("Cargando tipos de cita para dropdown.");
            ViewData["IdTipoCita"] = new SelectList(
                await _context.TiposCita.OrderBy(t => t.Nombre).ToListAsync(),
                "IdTipoCita", "Nombre", selectedTipo);
        }

        // Carga los estados posibles para el dropdown de edición de Admin/Vet
        private Task LoadEstadosCitaAsync(object? selectedEstado = null)
        {
            _logger.LogDebug("Cargando estados de cita para dropdown (Admin/Vet).");
            var estadosDisponibles = EstadoCita.GetEstadosEditables()
                                              .Select(e => new SelectListItem { Value = e, Text = e })
                                              .ToList();
            ViewData["EstadosCita"] = new SelectList(estadosDisponibles, "Value", "Text", selectedEstado);
            return Task.CompletedTask; // Es síncrono, pero lo devolvemos como Task para mantener patrón
        }

        private async Task<bool> CitaExists(int id)
        {
            return await _context.Citas.AnyAsync(e => e.IdCita == id);
        }
    }
}