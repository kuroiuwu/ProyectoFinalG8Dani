using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal_G8.ViewModels
{
    public class UsuarioEditViewModel
    {
        [Required]
        public int Id { get; set; } // Usar Id

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [DisplayName("Nombre Completo")]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        [StringLength(100)]
        [DisplayName("Correo Electrónico")]
        public string Email { get; set; } = null!; // Usar Email

        [Required(ErrorMessage = "El rol es obligatorio.")]
        [DisplayName("Rol")]
        public int IdRol { get; set; } // Para el rol principal

        [Phone(ErrorMessage = "Formato de teléfono inválido.")]
        [DisplayName("Teléfono")]
        [StringLength(20)]
        public string? PhoneNumber { get; set; } // Usar PhoneNumber

        [DisplayName("Dirección")]
        [StringLength(200)]
        public string? Direccion { get; set; }

        // NO incluir campos de contraseña aquí. La edición de contraseña
        // debe ser un proceso separado por seguridad.
    }
}