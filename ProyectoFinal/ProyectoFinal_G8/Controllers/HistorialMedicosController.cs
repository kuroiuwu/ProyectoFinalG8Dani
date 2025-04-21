using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;

namespace ProyectoFinal_G8.Controllers
{
    public class HistorialMedicosController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;

        public HistorialMedicosController(ProyectoFinal_G8Context context)
        {
            _context = context;
        }

        // GET: HistorialMedicos
        public async Task<IActionResult> Index()
        {
            var proyectoFinal_G8Context = _context.HistorialMedicos.Include(h => h.Mascota);
            return View(await proyectoFinal_G8Context.ToListAsync());
        }

        // GET: HistorialMedicos/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var historialMedico = await _context.HistorialMedicos
                .Include(h => h.Mascota)
                .FirstOrDefaultAsync(m => m.IdHistorial == id);
            if (historialMedico == null)
            {
                return NotFound();
            }

            return View(historialMedico);
        }

        // GET: HistorialMedicos/Create
        public IActionResult Create()
        {
            ViewData["IdMascota"] = new SelectList(_context.Mascotas, "IdMascota", "Nombre");
            return View();
        }

        // POST: HistorialMedicos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdHistorial,IdMascota,FechaRegistro,Descripcion,Tratamiento,Notas")] HistorialMedico historialMedico)
        {
            if (ModelState.IsValid)
            {
                _context.Add(historialMedico);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdMascota"] = new SelectList(_context.Mascotas, "IdMascota", "Nombre", historialMedico.IdMascota);
            return View(historialMedico);
        }

        // GET: HistorialMedicos/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var historialMedico = await _context.HistorialMedicos.FindAsync(id);
            if (historialMedico == null)
            {
                return NotFound();
            }
            ViewData["IdMascota"] = new SelectList(_context.Mascotas, "IdMascota", "Nombre", historialMedico.IdMascota);
            return View(historialMedico);
        }

        // POST: HistorialMedicos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdHistorial,IdMascota,FechaRegistro,Descripcion,Tratamiento,Notas")] HistorialMedico historialMedico)
        {
            if (id != historialMedico.IdHistorial)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(historialMedico);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HistorialMedicoExists(historialMedico.IdHistorial))
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
            ViewData["IdMascota"] = new SelectList(_context.Mascotas, "IdMascota", "Nombre", historialMedico.IdMascota);
            return View(historialMedico);
        }

        // GET: HistorialMedicos/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var historialMedico = await _context.HistorialMedicos
                .Include(h => h.Mascota)
                .FirstOrDefaultAsync(m => m.IdHistorial == id);
            if (historialMedico == null)
            {
                return NotFound();
            }

            return View(historialMedico);
        }

        // POST: HistorialMedicos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var historialMedico = await _context.HistorialMedicos.FindAsync(id);
            _context.HistorialMedicos.Remove(historialMedico);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool HistorialMedicoExists(int id)
        {
            return _context.HistorialMedicos.Any(e => e.IdHistorial == id);
        }
    }
}