using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using ProyectoFinal_G8.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace ProyectoFinal_G8.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RolsController : Controller
    {
        // Inyectar RoleManager Y UserManager
        private readonly RoleManager<Rol> _roleManager;
        private readonly UserManager<Usuario> _userManager; // <--- AÑADIDO

        // Modificar constructor para inyectar UserManager
        public RolsController(RoleManager<Rol> roleManager, UserManager<Usuario> userManager) // <--- AÑADIDO UserManager
        {
            _roleManager = roleManager;
            _userManager = userManager; // <--- AÑADIDO
        }

        // GET: Rols
        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(); // Ordenar alfabéticamente
            return View(roles);
        }

        // GET: Rols/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var rol = await _roleManager.FindByIdAsync(id.ToString());
            if (rol == null)
            {
                return NotFound();
            }
            return View(rol);
        }

        // GET: Rols/Create
        public IActionResult Create()
        {
            return View(new RolCreateViewModel());
        }

        // POST: Rols/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RolCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var rol = new Rol
                {
                    Name = viewModel.Name,
                    Descripcion = viewModel.Descripcion
                };
                var result = await _roleManager.CreateAsync(rol);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Rol creado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        if (error.Code == "DuplicateRoleName") { ModelState.AddModelError("Name", $"El nombre de rol '{viewModel.Name}' ya existe."); } else { ModelState.AddModelError(string.Empty, error.Description); }
                    }
                }
            }
            return View(viewModel);
        }

        // GET: Rols/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) { return NotFound(); }
            var rol = await _roleManager.FindByIdAsync(id.ToString());
            if (rol == null) { return NotFound(); }
            var viewModel = new RolEditViewModel { Id = rol.Id, Name = rol.Name, Descripcion = rol.Descripcion };
            return View(viewModel);
        }

        // POST: Rols/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RolEditViewModel viewModel)
        {
            if (id != viewModel.Id) { return NotFound(); }

            if (ModelState.IsValid)
            {
                var rol = await _roleManager.FindByIdAsync(id.ToString());
                if (rol == null) { ModelState.AddModelError(string.Empty, "El rol no fue encontrado."); return View(viewModel); }

                rol.Name = viewModel.Name;
                rol.Descripcion = viewModel.Descripcion;
                var result = await _roleManager.UpdateAsync(rol);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Rol actualizado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        if (error.Code == "DuplicateRoleName") { ModelState.AddModelError("Name", $"El nombre de rol '{viewModel.Name}' ya existe."); } else { ModelState.AddModelError(string.Empty, error.Description); }
                    }
                }
            }
            return View(viewModel);
        }

        // GET: Rols/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) { return NotFound(); }
            var rol = await _roleManager.FindByIdAsync(id.ToString());
            if (rol == null) { return NotFound(); }
            return View(rol);
        }

        // POST: Rols/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rol = await _roleManager.FindByIdAsync(id.ToString());
            if (rol == null) { TempData["ErrorMessage"] = "Rol no encontrado."; return RedirectToAction(nameof(Index)); }

            // Verificar si hay usuarios en este rol antes de eliminar
            // Ahora _userManager sí está disponible
            var usersInRole = await _userManager.GetUsersInRoleAsync(rol.Name);
            if (usersInRole.Any())
            {
                // Es mejor mostrar el error en la vista Delete en lugar de solo redirigir
                ModelState.AddModelError(string.Empty, $"No se puede eliminar el rol '{rol.Name}' porque tiene {usersInRole.Count} usuario(s) asignado(s).");
                return View("Delete", rol); // Devolver la vista Delete con el error
                // TempData["ErrorMessage"] = $"No se puede eliminar el rol '{rol.Name}' porque tiene usuarios asignados.";
                // return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.DeleteAsync(rol);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Rol eliminado exitosamente.";
            }
            else
            {
                TempData["ErrorMessage"] = "Error al eliminar el rol."; // Mensaje genérico
                ModelState.AddModelError(string.Empty, "Ocurrió un error al intentar eliminar el rol."); // Error para la vista si fallara por otra razón
                foreach (var error in result.Errors) { Console.WriteLine($"Error deleting role {id}: {error.Description}"); ModelState.AddModelError(string.Empty, error.Description); }
                return View("Delete", rol); // Devolver vista Delete si hay errores al borrar
            }

            return RedirectToAction(nameof(Index));
        }
    }
}