using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class DetalleFactura
    {
        [Key]
        public int IdDetalle { get; set; }

        [Required(ErrorMessage = "Debe pertenecer a una factura.")]
        [DisplayName("Factura")]
        public int IdFactura { get; set; }

        [ForeignKey("IdFactura")]
        public virtual Factura? Factura { get; set; }

        // Opcional: Si el detalle corresponde a un Insumo específico
        [DisplayName("Insumo")]
        public int? IdInsumo { get; set; }
        [ForeignKey("IdInsumo")]
        public virtual Insumo? Insumo { get; set; } // Requiere crear el modelo Insumo

        // Opcional: Si el detalle corresponde a un Tratamiento específico
        [DisplayName("Tratamiento")]
        public int? IdTratamiento { get; set; }
        [ForeignKey("IdTratamiento")]
        public virtual Tratamiento? Tratamiento { get; set; } // Requiere crear el modelo Tratamiento

        [Required(ErrorMessage = "La descripción del servicio o producto es obligatoria.")]
        [StringLength(200)]
        [DisplayName("Descripción")]
        public string DescripcionProductoServicio { get; set; } = null!;

        [Required(ErrorMessage = "La cantidad es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "El precio unitario es obligatorio.")]
        [DisplayName("Precio Unitario")]
        [Column(TypeName = "decimal(18, 2)")]
        [DataType(DataType.Currency)]
        public decimal PrecioUnitario { get; set; }

        [Required(ErrorMessage = "El subtotal es obligatorio.")]
        [DisplayName("Subtotal")]
        [Column(TypeName = "decimal(18, 2)")]
        [DataType(DataType.Currency)]
        public decimal Subtotal { get; set; } // Puede calcularse: Cantidad * PrecioUnitario
    }
}