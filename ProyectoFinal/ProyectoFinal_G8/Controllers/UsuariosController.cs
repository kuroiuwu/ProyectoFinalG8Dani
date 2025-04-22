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

namespace ProyectoFinal_G8.Controllers
{
    // Proteger todo el controlador para que solo usuarios con el rol "Admin" (o el que definas) puedan acceder
    [Authorize(Roles = "Admin")]
    public class UsuariosController : Controller
    {
        // Inyectar UserManager y RoleManager en lugar del DbContext directo para usuarios/roles
        private readonly UserManager<Usuario> _userManager;
        private readonly RoleManager<Rol> _roleManager;
     

        public UsuariosController(UserManager<Usuario> userManager, RoleManager<Rol> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            // _context = context; // Ya no es necesario para operaciones básicas de Usuario/Rol
        }

        // GET: Usuarios
        public async Task<IActionResult> Index()
        {
            // Obtener usuarios a través de UserManager
            // Include para cargar el Rol principal asociado (si mantienes la FK IdRol en Usuario)
            var usuarios = await _userManager.Users.Include(u => u.Rol).ToListAsync();
            return View(usuarios);
        }

        // GET: Usuarios/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Buscar usuario por ID usando UserManager
            var usuario = await _userManager.Users
                                        .Include(u => u.Rol) // Incluir rol principal
                                        .FirstOrDefaultAsync(u => u.Id == id); // Usar Id en lugar de IdUsuario

            if (usuario == null)
            {
                return NotFound();
            }

            // Opcional: Podrías mapear a un UsuarioDetailsViewModel aquí
            return View(usuario);
        }

        // GET: Usuarios/Create
        public async Task<IActionResult> Create()
        {
            // Cargar roles usando RoleManager para el dropdown
            await LoadRolesAsync(); // Usar método auxiliar para cargar roles
            return View(new UsuarioCreateViewModel()); // Pasar el ViewModel vacío a la vista
        }

        // POST: Usuarios/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsuarioCreateViewModel viewModel) // Recibir el ViewModel
        {
            if (ModelState.IsValid)
            {
                // Mapear ViewModel a la entidad Usuario
                var usuario = new Usuario
                {
                    Nombre = viewModel.Nombre,
                    UserName = viewModel.Email, // Usar Email como UserName (común)
                    Email = viewModel.Email,
                    PhoneNumber = viewModel.PhoneNumber,
                    Direccion = viewModel.Direccion,
                    IdRol = viewModel.IdRol, // Asignar el IdRol principal
                    EmailConfirmed = true, // Marcar como confirmado si no usas flujo de confirmación
                    PhoneNumberConfirmed = true // Marcar como confirmado si no usas flujo de confirmación
                };

                // Crear usuario usando UserManager (maneja hashing de contraseña)
                var result = await _userManager.CreateAsync(usuario, viewModel.Password);

                if (result.Succeeded)
                {
                    // Asignar el rol principal usando RoleManager/UserManager
                    // Obtener el nombre del rol seleccionado
                    var rol = await _roleManager.FindByIdAsync(viewModel.IdRol.ToString());
                    if (rol != null && rol.Name != null) // Asegurarse que el rol y su nombre existen
                    {
                        await _userManager.AddToRoleAsync(usuario, rol.Name);
                    }
                    else
                    {
                        // Log o manejo de error si el rol no se encuentra
                        ModelState.AddModelError("", "El rol seleccionado no es válido.");
                        await LoadRolesAsync(viewModel.IdRol); // Recargar roles
                        return View(viewModel);
                    }


                    TempData["SuccessMessage"] = "Usuario creado exitosamente."; // Mensaje de éxito opcional
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    // Si falla la creación, añadir errores al ModelState
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            // Si el ModelState no es válido o la creación falló, recargar roles y devolver la vista con el ViewModel
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

            // Buscar usuario por ID usando UserManager
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
            {
                return NotFound();
            }

            // Mapear entidad Usuario a UsuarioEditViewModel
            var viewModel = new UsuarioEditViewModel
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                PhoneNumber = usuario.PhoneNumber,
                Direccion = usuario.Direccion,
                IdRol = usuario.IdRol // Asignar el IdRol actual
            };

            // Cargar roles y pasar el ViewModel a la vista
            await LoadRolesAsync(usuario.IdRol);
            return View(viewModel);
        }

        // POST: Usuarios/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UsuarioEditViewModel viewModel) // Recibir el ViewModel
        {
            if (id != viewModel.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Buscar el usuario existente
                var usuario = await _userManager.FindByIdAsync(id.ToString());
                if (usuario == null)
                {
                    return NotFound();
                }

                // Guardar el rol anterior antes de actualizar
                var oldRoleId = usuario.IdRol;

                // Actualizar propiedades del usuario desde el ViewModel
                // NO actualizar contraseña aquí
                usuario.Nombre = viewModel.Nombre;
                usuario.Email = viewModel.Email;
                usuario.UserName = viewModel.Email; 
                usuario.PhoneNumber = viewModel.PhoneNumber;
                usuario.Direccion = viewModel.Direccion;
                usuario.IdRol = viewModel.IdRol; 

                // Guardar cambios usando UserManager
                var result = await _userManager.UpdateAsync(usuario);

                if (result.Succeeded)
                {
                    // Si el rol principal cambió, actualizar la asignación de roles de Identity
                    if (oldRoleId != viewModel.IdRol)
                    {
                        var oldRole = await _roleManager.FindByIdAsync(oldRoleId.ToString());
                        var newRole = await _roleManager.FindByIdAsync(viewModel.IdRol.ToString());

                        // Quitar rol anterior (si existía)
                        if (oldRole != null && oldRole.Name != null && await _userManager.IsInRoleAsync(usuario, oldRole.Name))
                        {
                            await _userManager.RemoveFromRoleAsync(usuario, oldRole.Name);
                        }
                        // Añadir rol nuevo (si existe)
                        if (newRole != null && newRole.Name != null)
                        {
                            await _userManager.AddToRoleAsync(usuario, newRole.Name);
                        }
                    }

                    TempData["SuccessMessage"] = "Usuario actualizado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    // Si falla la actualización, añadir errores al ModelState
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            // Si el ModelState no es válido o la actualización falló, recargar roles y devolver la vista con el ViewModel
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

            // Buscar usuario por ID usando UserManager
            var usuario = await _userManager.Users
                                       .Include(u => u.Rol) // Incluir rol principal para mostrarlo
                                       .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
            {
                return NotFound();
            }

            // Opcional: Podrías mapear a un UsuarioDeleteViewModel
            return View(usuario);
        }

        // POST: Usuarios/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Buscar usuario por ID usando UserManager
            var usuario = await _userManager.FindByIdAsync(id.ToString());
            if (usuario == null)
            {
                TempData["ErrorMessage"] = "Usuario no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            // Eliminar usuario usando UserManager
            var result = await _userManager.DeleteAsync(usuario);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Usuario eliminado exitosamente.";
            }
            else
            {
                // Si falla la eliminación, añadir errores a TempData o loggear
                TempData["ErrorMessage"] = "Error al eliminar el usuario.";
                foreach (var error in result.Errors)
                {
                    // Loggear error.Description
                    Console.WriteLine($"Error deleting user {id}: {error.Description}"); // Log simple a consola
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // Método auxiliar para cargar la lista de roles en ViewBag
        private async Task LoadRolesAsync(object? selectedRole = null)
        {
            // Usar RoleManager para obtener roles. Seleccionar Id y Name (propiedad de IdentityRole)
            ViewBag.ListaRoles = new SelectList(await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(), "Id", "Name", selectedRole);
        }

        
    }
}