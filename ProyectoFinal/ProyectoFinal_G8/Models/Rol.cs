using Microsoft.AspNetCore.Identity; // <-- Necesario
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal_G8.Models
{
    // Heredar de IdentityRole<int> ya que tu PK original (IdRol) era int
    public class Rol : IdentityRole<int>
    {
        // [Key] // La Key 'Id' es heredada de IdentityRole<int>
        // public int IdRol { get; set; }

        // 'NombreRol' se mapea a la propiedad 'Name' heredada de IdentityRole
        // Los atributos [Required], [StringLength] se aplicarán a 'Name'.
        // [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
        // [StringLength(50)]
        // [DisplayName("Nombre del Rol")]
        // public string NombreRol { get; set; } = null!; // Usa la propiedad 'Name' heredada

        // Propiedad personalizada que quieres mantener:
        [StringLength(200)]
        [DisplayName("Descripción")]
        public string? Descripcion { get; set; }

        // --- Relación de Navegación (Se mantiene para referencia, pero Identity usa UserRoles) ---
        public virtual ICollection<Usuario>? Usuarios { get; set; }

        // --- Notas Adicionales ---
        // 1. La propiedad 'Name' (heredada) reemplaza a 'NombreRol'.
        // 2. IdentityRole tiene 'NormalizedName' y 'ConcurrencyStamp'.
    }
}