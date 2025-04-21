using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using Microsoft.AspNetCore.Identity;      // <-- Añadir using para Identity
using Microsoft.AspNetCore.Authorization;  // <-- Añadir using para Autorización

namespace ProyectoFinal_G8.Controllers
{
    [Authorize] // <-- Proteger controlador (ajusta roles si es necesario, ej. [Authorize(Roles="Admin,Veterinario")])
    public class CitasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager; // <-- Inyectar UserManager

        public CitasController(ProyectoFinal_G8Context context, UserManager<Usuario> userManager) // <-- Modificar constructor
        {
            _context = context;
            _userManager = userManager; // <-- Asignar UserManager
        }

        // GET: Citas
        public async Task<IActionResult> Index()
        {
            // Incluir Mascota y Veterinario (Usuario) usando la navegación definida
            var citas = _context.Citas
                                .Include(c => c.Mascota)
                                .Include(c => c.Veterinario); // Asume que 'Veterinario' es la prop. de navegación para IdUsuarioVeterinario
            return View(await citas.ToListAsync());
        }

        // GET: Citas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cita = await _context.Citas
                .Include(c => c.Mascota)
                .Include(c => c.Veterinario)
                .FirstOrDefaultAsync(m => m.IdCita == id);
            if (cita == null)
            {
                return NotFound();
            }

            return View(cita);
        }

        // GET: Citas/Create
        public async Task<IActionResult> Create() // <-- Cambiar a async Task
        {
            await LoadVeterinariosAsync(); // Cargar lista de veterinarios
            ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre"); // Cargar mascotas
            return View();
        }

        // POST: Citas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,Motivo,Estado,Notas")] Cita cita)
        {
            // Remover validación de navegación si causa problemas con ModelState
            ModelState.Remove("Mascota");
            ModelState.Remove("Veterinario");

            if (ModelState.IsValid)
            {
                _context.Add(cita);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cita creada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            // Si falla, recargar listas
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre", cita.IdMascota);
            return View(cita);
        }

        // GET: Citas/Edit/5
        public async Task<IActionResult> Edit(int? id) // <-- Cambiar a async Task
        {
            if (id == null)
            {
                return NotFound();
            }

            var cita = await _context.Citas.FindAsync(id);
            if (cita == null)
            {
                return NotFound();
            }
            // Cargar listas
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre", cita.IdMascota);
            return View(cita);
        }

        // POST: Citas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdCita,FechaHora,IdMascota,IdUsuarioVeterinario,Motivo,Estado,Notas")] Cita cita)
        {
            if (id != cita.IdCita)
            {
                return NotFound();
            }

            ModelState.Remove("Mascota");
            ModelState.Remove("Veterinario");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cita);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Cita actualizada exitosamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await CitaExists(cita.IdCita)) // <-- Usar await
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            // Si falla, recargar listas
            await LoadVeterinariosAsync(cita.IdUsuarioVeterinario);
            ViewData["IdMascota"] = new SelectList(await _context.Mascotas.OrderBy(m => m.Nombre).ToListAsync(), "IdMascota", "Nombre", cita.IdMascota);
            return View(cita);
        }

        // GET: Citas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cita = await _context.Citas
                .Include(c => c.Mascota)
                .Include(c => c.Veterinario)
                .FirstOrDefaultAsync(m => m.IdCita == id);
            if (cita == null)
            {
                return NotFound();
            }

            return View(cita);
        }

        // POST: Citas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cita = await _context.Citas.FindAsync(id);
            if (cita != null) // Verificar si se encontró la cita
            {
                _context.Citas.Remove(cita);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cita eliminada exitosamente.";
            }
            else
            {
                TempData["ErrorMessage"] = "Cita no encontrada.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para cargar Veterinarios
        private async Task LoadVeterinariosAsync(object? selectedVeterinario = null)
        {
            // ¡Asegúrate que el rol "Veterinario" existe!
            var veterinarios = await _userManager.GetUsersInRoleAsync("Veterinario");
            // Usar Id (PK de Usuario/IdentityUser) y Nombre (propiedad personalizada)
            ViewData["IdUsuarioVeterinario"] = new SelectList(veterinarios.OrderBy(u => u.Nombre), "Id", "Nombre", selectedVeterinario);
        }

        private async Task<bool> CitaExists(int id) // <-- Cambiar a async Task
        {
            return await _context.Citas.AnyAsync(e => e.IdCita == id); // <-- Usar AnyAsync
        }
    }
}