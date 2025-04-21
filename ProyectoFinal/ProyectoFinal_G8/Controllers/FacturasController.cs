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
    public class FacturasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager; // <-- Inyectar UserManager

        public FacturasController(ProyectoFinal_G8Context context, UserManager<Usuario> userManager) // <-- Modificar constructor
        {
            _context = context;
            _userManager = userManager; // <-- Asignar UserManager
        }

        // GET: Facturas
        public async Task<IActionResult> Index()
        {
            // Incluir Cliente (Usuario) usando la navegación definida
            var facturas = _context.Facturas
                                 .Include(f => f.Cliente); // Asume que 'Cliente' es la prop. de navegación para IdUsuarioCliente
            return View(await facturas.ToListAsync());
        }

        // GET: Facturas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura = await _context.Facturas
                .Include(f => f.Cliente)
                .Include(f => f.DetallesFactura)
                    .ThenInclude(d => d.Insumo) // Incluir Insumo desde Detalles
                .Include(f => f.DetallesFactura)
                    .ThenInclude(d => d.Tratamiento) // Incluir Tratamiento desde Detalles
                .FirstOrDefaultAsync(m => m.IdFactura == id);
            if (factura == null)
            {
                return NotFound();
            }

            return View(factura);
        }

        // GET: Facturas/Create
        public async Task<IActionResult> Create() // <-- Cambiar a async Task
        {
            await LoadClientesAsync(); // Cargar lista de clientes
            return View();
        }

        // POST: Facturas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdFactura,IdUsuarioCliente,FechaEmision,MontoTotal,Estado")] Factura factura)
        {
            ModelState.Remove("Cliente"); // Evitar validación de navegación
            ModelState.Remove("DetallesFactura"); // Evitar validación de navegación

            if (ModelState.IsValid)
            {
                // Podrías añadir lógica para calcular MontoTotal basado en detalles si fuera necesario
                factura.FechaEmision = DateTime.Now; // Asignar fecha actual al crear
                _context.Add(factura);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Factura creada exitosamente.";
                // Considera redirigir a Details para añadir detalles de factura
                return RedirectToAction(nameof(Index));
            }
            // Si falla, recargar lista de clientes
            await LoadClientesAsync(factura.IdUsuarioCliente);
            return View(factura);
        }

        // GET: Facturas/Edit/5
        public async Task<IActionResult> Edit(int? id) // <-- Cambiar a async Task
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura = await _context.Facturas.FindAsync(id);
            if (factura == null)
            {
                return NotFound();
            }
            // Cargar lista de clientes
            await LoadClientesAsync(factura.IdUsuarioCliente);
            return View(factura);
        }

        // POST: Facturas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdFactura,IdUsuarioCliente,FechaEmision,MontoTotal,Estado")] Factura factura)
        {
            if (id != factura.IdFactura)
            {
                return NotFound();
            }

            ModelState.Remove("Cliente");
            ModelState.Remove("DetallesFactura");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(factura);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Factura actualizada exitosamente.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await FacturaExists(factura.IdFactura)) // <-- Usar await
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
            // Si falla, recargar lista de clientes
            await LoadClientesAsync(factura.IdUsuarioCliente);
            return View(factura);
        }

        // GET: Facturas/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var factura = await _context.Facturas
                .Include(f => f.Cliente) // Incluir cliente para mostrar info
                .FirstOrDefaultAsync(m => m.IdFactura == id);
            if (factura == null)
            {
                return NotFound();
            }

            return View(factura);
        }

        // POST: Facturas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Considera borrar DetallesFactura asociados o manejar la restricción FK
            var factura = await _context.Facturas.FindAsync(id);
            if (factura != null)
            {
                _context.Facturas.Remove(factura);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Factura eliminada exitosamente.";
            }
            else
            {
                TempData["ErrorMessage"] = "Factura no encontrada.";
            }

            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para cargar Clientes
        private async Task LoadClientesAsync(object? selectedCliente = null)
        {
            // Obtener todos los usuarios o filtrar por rol "Cliente" si lo tienes
            // var clientes = await _userManager.GetUsersInRoleAsync("Cliente");
            var clientes = await _userManager.Users.ToListAsync();
            // Usar Id (PK de Usuario) y Nombre (propiedad personalizada)
            ViewData["IdUsuarioCliente"] = new SelectList(clientes.OrderBy(u => u.Nombre), "Id", "Nombre", selectedCliente);
        }

        private async Task<bool> FacturaExists(int id) // <-- Cambiar a async Task
        {
            return await _context.Facturas.AnyAsync(e => e.IdFactura == id); // <-- Usar AnyAsync
        }
    }
}