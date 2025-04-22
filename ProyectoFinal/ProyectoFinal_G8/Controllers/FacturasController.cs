using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using Microsoft.AspNetCore.Identity;     
using Microsoft.AspNetCore.Authorization; 
namespace ProyectoFinal_G8.Controllers
{
    [Authorize] // Protege todo el controlador, requiere que el usuario esté autenticado
    public class FacturasController : Controller
    {
        private readonly ProyectoFinal_G8Context _context;
        private readonly UserManager<Usuario> _userManager; // Servicio para manejar usuarios de Identity

        // Inyección de dependencias del DbContext y UserManager
        public FacturasController(ProyectoFinal_G8Context context, UserManager<Usuario> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Facturas
        public async Task<IActionResult> Index()
        {
            // Obtiene todas las facturas incluyendo la información del cliente asociado
            var facturas = _context.Facturas
                                   .Include(f => f.Cliente); // 'Cliente' es la propiedad de navegación hacia Usuario
            return View(await facturas.ToListAsync());
        }

        // GET: Facturas/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Obtiene la factura por ID, incluyendo Cliente y Detalles con sus Insumos/Tratamientos
            var factura = await _context.Facturas
                .Include(f => f.Cliente) // Carga el Usuario (Cliente) relacionado
                .Include(f => f.DetallesFactura) // Carga la colección de detalles
                    .ThenInclude(d => d.Insumo) // Dentro de cada detalle, carga el Insumo
                .Include(f => f.DetallesFactura) // Vuelve a empezar desde DetallesFactura
                    .ThenInclude(d => d.Tratamiento) // Dentro de cada detalle, carga el Tratamiento
                .FirstOrDefaultAsync(m => m.IdFactura == id);

            if (factura == null)
            {
                return NotFound();
            }

            return View(factura);
        }

        // GET: Facturas/Create
        public async Task<IActionResult> Create()
        {
            await LoadClientesAsync(); // Carga la lista de clientes para el dropdown
            return View();
        }

        // POST: Facturas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdFactura,IdUsuarioCliente,FechaEmision,MontoTotal,Estado")] Factura factura)
        {
            // Evita que el ModelState intente validar las propiedades de navegación que no vienen del form
            ModelState.Remove("Cliente");
            ModelState.Remove("DetallesFactura");

            if (ModelState.IsValid)
            {
                factura.FechaEmision = DateTime.Now; // Asigna la fecha actual al crear la factura
                _context.Add(factura);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Factura creada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            // Si el modelo no es válido, recargar la lista de clientes y mostrar la vista de nuevo
            await LoadClientesAsync(factura.IdUsuarioCliente);
            return View(factura);
        }

        // GET: Facturas/Edit/5
        public async Task<IActionResult> Edit(int? id)
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
            // Carga la lista de clientes para el dropdown, seleccionando el actual
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

            // Evita que el ModelState intente validar las propiedades de navegación
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
                    // Comprueba si la factura aún existe si ocurre un error de concurrencia
                    if (!await FacturaExists(factura.IdFactura))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw; // Relanza la excepción si la factura existe pero hubo otro error
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            // Si el modelo no es válido, recargar la lista de clientes y mostrar la vista de nuevo
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

            // Obtiene la factura incluyendo al cliente para mostrar su información en la confirmación
            var factura = await _context.Facturas
                .Include(f => f.Cliente)
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

        // Método auxiliar para cargar la lista de Clientes (Usuarios) para SelectList
        private async Task LoadClientesAsync(object? selectedCliente = null)
        {
            // Obtiene todos los usuarios registrados. Considerar filtrar por rol si aplica.
            var clientes = await _userManager.Users.ToListAsync();
            // Prepara el SelectList para la vista: Valor = Id del usuario, Texto = Nombre del usuario
            ViewData["IdUsuarioCliente"] = new SelectList(clientes.OrderBy(u => u.Nombre), "Id", "Nombre", selectedCliente);
        }

        // Método auxiliar para verificar si una Factura existe por su ID
        private async Task<bool> FacturaExists(int id)
        {
            return await _context.Facturas.AnyAsync(e => e.IdFactura == id);
        }
    }
}