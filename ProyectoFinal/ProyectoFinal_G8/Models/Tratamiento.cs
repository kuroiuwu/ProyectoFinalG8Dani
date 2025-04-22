using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class Tratamiento
    {
        [Key]
        public int IdTratamiento { get; set; }

        [Required(ErrorMessage = "El nombre del tratamiento es obligatorio.")]
        [StringLength(150)]
        [DisplayName("Nombre del Tratamiento")]
        public string Nombre { get; set; } = null!;

        [StringLength(500)]
        [DisplayName("Descripción")]
        public string? Descripcion { get; set; }

        [DisplayName("Costo Estándar")]
        [Column(TypeName = "decimal(18, 2)")]
        [DataType(DataType.Currency)]
        [Range(0, double.MaxValue, ErrorMessage = "El costo no puede ser negativo.")]
        public decimal? Costo { get; set; }

        // Relación de Navegación (si se usa en facturas)
        public virtual ICollection<DetalleFactura>? DetallesFactura { get; set; }

    }
}