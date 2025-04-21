// Areas/Identity/Pages/Account/Register.cshtml.cs

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using ProyectoFinal_G8.Models;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore; // Probablemente aún necesario

namespace ProyectoFinal_G8.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<Usuario> _signInManager;
        private readonly UserManager<Usuario> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<Rol> _roleManager; // <-- RE-AÑADIR RoleManager

        public RegisterModel(
            UserManager<Usuario> userManager,
            SignInManager<Usuario> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            RoleManager<Rol> roleManager) // <-- RE-AÑADIR RoleManager al constructor
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _roleManager = roleManager; // <-- RE-AÑADIR asignación
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        // Ya no se necesita la propiedad RoleList aquí

        public class InputModel
        {
            [Required(ErrorMessage = "El nombre es obligatorio.")]
            [StringLength(100)]
            [Display(Name = "Nombre Completo")]
            public string Nombre { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Correo Electrónico")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} y máximo {1} caracteres.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Contraseña")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirmar Contraseña")]
            [Compare("Password", ErrorMessage = "La contraseña y la confirmación no coinciden.")]
            public string ConfirmPassword { get; set; }

            // Ya no está IdRol aquí, se asigna automáticamente
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // --- BUSCAR EL ID DEL ROL "Cliente" ---
                var clienteRole = await _roleManager.FindByNameAsync("Cliente");
                if (clienteRole == null)
                {
                    // El rol "Cliente" no existe! El Seeding falló o fue modificado.
                    ModelState.AddModelError(string.Empty, "Error interno: El rol de cliente predeterminado no está configurado.");
                    _logger.LogError("El rol 'Cliente' no fue encontrado en la base de datos durante el registro.");
                    return Page(); // Detener el proceso si el rol base no existe
                }
                // ---------------------------------------

                var user = new Usuario
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    Nombre = Input.Nombre,
                    IdRol = clienteRole.Id, // <-- ASIGNAR EL ID DEL ROL CLIENTE ENCONTRADO
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = false
                };

                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Usuario creado con contraseña.");

                    // Asignar el rol de Identity (esto afecta a AspNetUserRoles)
                    // Es un poco redundante si ya asignaste IdRol, pero es la forma estándar de Identity
                    var roleResult = await _userManager.AddToRoleAsync(user, "Cliente");
                    if (!roleResult.Succeeded)
                    {
                        _logger.LogError($"Error al asignar rol 'Cliente' (Identity) al usuario {user.UserName}.");
                        // Manejar este error si es necesario
                    }
                    else
                    {
                        _logger.LogInformation($"Usuario {user.UserName} asignado al rol Cliente (Identity).");
                    }

                    // Código de envío de email comentado
                    /* ... */

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // Si llegamos aquí, algo falló, mostrar formulario de nuevo
            return Page();
        }
    }
}