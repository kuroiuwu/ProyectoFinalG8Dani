using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class Insumo // (Suministro/Inventario)
    {
        [Key]
        public int IdInsumo { get; set; }

        [Required(ErrorMessage = "El nombre del insumo es obligatorio.")]
        [StringLength(150)]
        [DisplayName("Nombre del Insumo")]
        public string Nombre { get; set; } = null!;

        [StringLength(500)]
        [DisplayName("Descripción")]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "La cantidad en stock es obligatoria.")]
        [DisplayName("Cantidad en Stock")]
        [Range(0, int.MaxValue, ErrorMessage = "La cantidad no puede ser negativa.")]
        public int CantidadStock { get; set; }

        [Required(ErrorMessage = "La unidad de medida es obligatoria.")]
        [StringLength(30)]
        [DisplayName("Unidad de Medida")] // Ej: Unidad, Caja, Ml, Kg
        public string UnidadMedida { get; set; } = null!;

        [DisplayName("Umbral Bajo Stock")]
        [Range(0, int.MaxValue, ErrorMessage = "El umbral no puede ser negativo.")]
        public int? UmbralBajoStock { get; set; } // Para notificaciones

        [DisplayName("Precio Costo Unitario")]
        [Column(TypeName = "decimal(18, 2)")]
        [DataType(DataType.Currency)]
        public decimal? PrecioCosto { get; set; }

        [DisplayName("Precio Venta Unitario")]
        [Column(TypeName = "decimal(18, 2)")]
        [DataType(DataType.Currency)]
        public decimal? PrecioVenta { get; set; }

        // Relación de Navegación (si se usa en facturas)
        public virtual ICollection<DetalleFactura>? DetallesFactura { get; set; }
    }
}