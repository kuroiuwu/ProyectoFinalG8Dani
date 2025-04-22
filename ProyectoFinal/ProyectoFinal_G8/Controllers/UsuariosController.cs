using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoFinal_G8.Models;
using ProyectoFinal_G8.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging; // Necesario para ILogger si lo inyectas

namespace ProyectoFinal_G8.Controllers
{
    [Authorize(Roles = "Admin")] // Solo Admin puede acceder
    public class UsuariosController : Controller
    {
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Rol> _roleManager;
        private readonly ILogger<UsuariosController> _logger; // Añadido Logger

        public UsuariosController(
            UserManager<Usuario> userManager,
            RoleManager<Rol> roleManager,
            ILogger<UsuariosController> logger) // Inyectar Logger
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger; // Guardar instancia del logger
        }

        // GET: Usuarios
        public async Task<IActionResult> Index(string? searchString) // Añadido parámetro de búsqueda
        {
            ViewData["CurrentFilter"] = searchString; // Pasar filtro actual a la vista

            var usersQuery = _userManager.Users.Include(u => u.Rol).AsQueryable(); // Empezar con IQueryable

            if (!String.IsNullOrEmpty(searchString))
            {
                // Filtrar por Nombre (insensible a mayúsculas/minúsculas)
                // O Email si prefieres buscar por email tambien: || u.Email.Contains(searchString)
                usersQuery = usersQuery.Where(u => u.Nombre.Contains(searchString));
                _logger.LogInformation($"Buscando usuarios con nombre que contenga: '{searchString}'");
            }

            // Ordenar y ejecutar la consulta
            var usuarios = await usersQuery.OrderBy(u => u.Nombre).ToListAsync();

            return View(usuarios);
        }

        // GET: Usuarios/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var usuario = await _userManager.Users
                                        .Include(u => u.Rol)
                                        .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
            {
                return NotFound();
            }

            return View(usuario);
        }

        // GET: Usuarios/Create
        public async Task<IActionResult> Create()
        {
            await LoadRolesAsync();
            return View(new UsuarioCreateViewModel());
        }

        // POST: Usuarios/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsuarioCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                var usuario = new Usuario
                {
                    Nombre = viewModel.Nombre,
                    UserName = viewModel.Email,
                    Email = viewModel.Email,
                    PhoneNumber = viewModel.PhoneNumber,
                    Direccion = viewModel.Direccion,
                    IdRol = viewModel.IdRol,
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = true
                };

                var result = await _userManager.CreateAsync(usuario, viewModel.Password);

                if (result.Succeeded)
                {
                    var rol = await _roleManager.FindByIdAsync(viewModel.IdRol.ToString());
                    if (rol != null && rol.Name != null)
                    {
                        await _userManager.AddToRoleAsync(usuario, rol.Name);
                    }
                    else
                    {
                        ModelState.AddModelError("", "El rol seleccionado no es válido.");
                        _logger.LogWarning($"Intento de crear usuario con rol inválido ID: {viewModel.IdRol}");
                        await LoadRolesAsync(viewModel.IdRol);
                        return View(viewModel);
                    }

                    TempData["SuccessMessage"] = "Usuario creado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                        _logger.LogWarning($"Error creando usuario: {error.Description}");
                    }
                }
            }

            await LoadRolesAsync(viewModel.IdRol);
            return View(viewModel);
        }

        // GET: Usuarios/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
            {
                return NotFound();
            }

            var viewModel = new UsuarioEditViewModel
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                PhoneNumber = usuario.PhoneNumber,
                Direccion = usuario.Direccion,
                IdRol = usuario.IdRol
            };

            await LoadRolesAsync(usuario.IdRol);
            return View(viewModel);
        }

        // POST: Usuarios/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UsuarioEditViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var usuario = await _userManager.FindByIdAsync(id.ToString());
                if (usuario == null)
                {
                    return NotFound();
                }

                var oldRoleId = usuario.IdRol;

                usuario.Nombre = viewModel.Nombre;
                usuario.Email = viewModel.Email;
                usuario.UserName = viewModel.Email;
                usuario.PhoneNumber = viewModel.PhoneNumber;
                usuario.Direccion = viewModel.Direccion;
                usuario.IdRol = viewModel.IdRol;

                var result = await _userManager.UpdateAsync(usuario);

                if (result.Succeeded)
                {
                    if (oldRoleId != viewModel.IdRol)
                    {
                        var oldRole = await _roleManager.FindByIdAsync(oldRoleId.ToString());
                        var newRole = await _roleManager.FindByIdAsync(viewModel.IdRol.ToString());

                        // Quitar rol anterior si existía y el usuario lo tenía
                        if (oldRole?.Name != null && await _userManager.IsInRoleAsync(usuario, oldRole.Name))
                        {
                            await _userManager.RemoveFromRoleAsync(usuario, oldRole.Name);
                        }
                        // Añadir rol nuevo si existe
                        if (newRole?.Name != null)
                        {
                            await _userManager.AddToRoleAsync(usuario, newRole.Name);
                        }
                        _logger.LogInformation($"Rol del usuario {id} cambiado de {oldRole?.Name ?? "Ninguno"} a {newRole?.Name ?? "Ninguno"}");
                    }

                    TempData["SuccessMessage"] = "Usuario actualizado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                        _logger.LogWarning($"Error actualizando usuario {id}: {error.Description}");
                    }
                }
            }

            await LoadRolesAsync(viewModel.IdRol);
            return View(viewModel);
        }

        // GET: Usuarios/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var usuario = await _userManager.Users
                                        .Include(u => u.Rol)
                                        .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
            {
                return NotFound();
            }

            return View(usuario);
        }

        // POST: Usuarios/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
            {
                TempData["ErrorMessage"] = "Usuario no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            // *** Opcional: Evitar borrar el propio usuario Admin? ***
            // var currentUser = await _userManager.GetUserAsync(User);
            // if (currentUser != null && currentUser.Id == id)
            // {
            //      TempData["ErrorMessage"] = "No puedes eliminar tu propia cuenta de administrador.";
            //      return RedirectToAction(nameof(Index));
            // }

            var result = await _userManager.DeleteAsync(usuario);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Usuario eliminado exitosamente.";
                _logger.LogWarning($"Usuario ID {id} eliminado por {User.Identity?.Name}.");
            }
            else
            {
                TempData["ErrorMessage"] = "Error al eliminar el usuario.";
                foreach (var error in result.Errors)
                {
                    _logger.LogError($"Error eliminando usuario {id}: {error.Description}");
                }
                // Podríamos redirigir a la vista Delete con un mensaje si falla
                // return RedirectToAction(nameof(Delete), new { id = id });
            }

            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para cargar roles
        private async Task LoadRolesAsync(object? selectedRole = null)
        {
            ViewBag.ListaRoles = new SelectList(await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(), "Id", "Name", selectedRole);
        }
    }
}