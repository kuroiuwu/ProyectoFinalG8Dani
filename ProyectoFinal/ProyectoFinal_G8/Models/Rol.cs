using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal_G8.Models
{
    public class Rol
    {
        [Key]
        public int IdRol { get; set; }

        [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
        [StringLength(50)]
        [DisplayName("Nombre del Rol")]
        public string NombreRol { get; set; } = null!;

        [StringLength(200)]
        [DisplayName("Descripción")]
        public string? Descripcion { get; set; }

        // Relación de Navegación
        public virtual ICollection<Usuario>? Usuarios { get; set; }
    }
}