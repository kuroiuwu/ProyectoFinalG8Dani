using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal_G8.ViewModels
{
    public class UsuarioCreateViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [DisplayName("Nombre Completo")]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        [StringLength(100)]
        [DisplayName("Correo Electrónico")]
        public string Email { get; set; } = null!; // Usar Email

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(100, ErrorMessage = "La {0} debe tener al menos {2} y máximo {1} caracteres.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [DisplayName("Contraseña")]
        public string Password { get; set; } = null!; // Campo para la contraseña

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "La contraseña y la confirmación no coinciden.")]
        [DisplayName("Confirmar Contraseña")]
        public string ConfirmPassword { get; set; } = null!; // Campo para confirmar contraseña

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
    }
}