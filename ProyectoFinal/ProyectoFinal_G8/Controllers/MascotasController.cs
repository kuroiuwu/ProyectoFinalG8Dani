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
    [Authorize] // <-- Proteger controlador (ajusta roles si es necesario)
    public class MascotasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager; // <-- Inyectar UserManager

        public MascotasController(ProyectoFinal_G8Context context, UserManager<Usuario> userManager) // <-- Modificar constructor
        {
            _context = context;
            _userManager = userManager; // <-- Asignar UserManager
        }

        // GET: Mascotas
        public async Task<IActionResult> Index(string searchString)
        {
            ViewData["CurrentFilter"] = searchString;

            var mascotas = from m in _context.Mascotas
                           select m;

            if (!string.IsNullOrEmpty(searchString))
            {
                mascotas = mascotas.Where(s => s.Nombre.Contains(searchString));
            }

            var mascotaList = await mascotas.ToListAsync();

            if (!mascotaList.Any())
            {
                ViewData["NoResultsMessage"] = "No se encontró ninguna mascota con ese nombre.";
            }

            return View(mascotaList);
        }


        // GET: Mascotas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var mascota = await _context.Mascotas
                .Include(m => m.Dueño) // Incluir dueño para mostrar info
                 .Include(m => m.Citas) // Incluir Citas si las muestras en Detalles
                 .Include(m => m.HistorialesMedicos) // Incluir Historial si lo muestras
                .FirstOrDefaultAsync(m => m.IdMascota == id);
            if (mascota == null)
            {
                return NotFound();
            }

            return View(mascota);
        }

        // GET: Mascotas/Create
        public async Task<IActionResult> Create() // <-- Cambiar a async Task
        {
            await LoadDueñosAsync(); // Cargar lista de dueños (usuarios)
            return View();
        }

        // POST: Mascotas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdMascota,Nombre,Especie,Raza,FechaNacimiento,IdUsuarioDueño")] Mascota mascota)
        {
            ModelState.Remove("Dueño"); // Evitar validación de navegación
            ModelState.Remove("Citas");
            ModelState.Remove("HistorialesMedicos");


            if (ModelState.IsValid)
            {
                _context.Add(mascota);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Mascota registrada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            // Si falla, recargar lista de dueños
            await LoadDueñosAsync(mascota.IdUsuarioDueño);
            return View(mascota);
        }

        // GET: Mascotas/Edit/5
        public async Task<IActionResult> Edit(int? id) // <-- Cambiar a async Task
        {
            if (id == null)
            {
                return NotFound();
            }

            var mascota = await _context.Mascotas.FindAsync(id);
            if (mascota == null)
            {
                return NotFound();
            }
            // Cargar lista de dueños
            await LoadDueñosAsync(mascota.IdUsuarioDueño);
            return View(mascota);
        }

        // POST: Mascotas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdMascota,Nombre,Especie,Raza,FechaNacimiento,IdUsuarioDueño")] Mascota mascota)
        {
            if (id != mascota.IdMascota)
            {
                return NotFound();
            }

            ModelState.Remove("Dueño");
            ModelState.Remove("Citas");
            ModelState.Remove("HistorialesMedicos");

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
                    if (!await MascotaExists(mascota.IdMascota)) // <-- Usar await
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
            // Si falla, recargar lista de dueños
            await LoadDueñosAsync(mascota.IdUsuarioDueño);
            return View(mascota);
        }

        // GET: Mascotas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var mascota = await _context.Mascotas
                .Include(m => m.Dueño) // Incluir dueño para mostrar info
                .FirstOrDefaultAsync(m => m.IdMascota == id);
            if (mascota == null)
            {
                return NotFound();
            }

            return View(mascota);
        }

        // POST: Mascotas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Considera laFK en Citas, HistorialMedico (Cascade/Restrict)
            var mascota = await _context.Mascotas.FindAsync(id);
            if (mascota != null)
            {
                _context.Mascotas.Remove(mascota);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Mascota eliminada exitosamente.";
            }
            else
            {
                TempData["ErrorMessage"] = "Mascota no encontrada.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para cargar Dueños (Usuarios)
        private async Task LoadDueñosAsync(object? selectedDueño = null)
        {
            // Obtener todos los usuarios o filtrar por rol "Cliente"
            // var dueños = await _userManager.GetUsersInRoleAsync("Cliente");
            var dueños = await _userManager.Users.ToListAsync();
            // Usar Id (PK de Usuario) y Nombre (propiedad personalizada)
            ViewData["IdUsuarioDueño"] = new SelectList(dueños.OrderBy(u => u.Nombre), "Id", "Nombre", selectedDueño);
        }


        private async Task<bool> MascotaExists(int id) // <-- Cambiar a async Task
        {
            return await _context.Mascotas.AnyAsync(e => e.IdMascota == id); // <-- Usar AnyAsync
        }
    }
}