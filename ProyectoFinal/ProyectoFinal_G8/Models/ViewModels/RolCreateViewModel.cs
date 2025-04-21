using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal_G8.ViewModels
{
    public class RolCreateViewModel
    {
        [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
        [StringLength(50, ErrorMessage = "El nombre no puede exceder los 50 caracteres.")]
        [DisplayName("Nombre del Rol")]
        public string Name { get; set; } = null!;

        [StringLength(200, ErrorMessage = "La descripción no puede exceder los 200 caracteres.")]
        [DisplayName("Descripción")]
        public string? Descripcion { get; set; }
    }
}