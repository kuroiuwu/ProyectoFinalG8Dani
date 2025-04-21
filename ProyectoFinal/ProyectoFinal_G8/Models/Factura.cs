using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class Factura
    {
        [Key]
        public int IdFactura { get; set; }

        [Required(ErrorMessage = "Debe asociar la factura a un cliente.")]
        [DisplayName("Cliente")]
        public int IdUsuarioCliente { get; set; }

        [ForeignKey("IdUsuarioCliente")]
        public virtual Usuario? Cliente { get; set; }

        [Required(ErrorMessage = "La fecha de emisión es obligatoria.")]
        [DisplayName("Fecha Emisión")]
        [DataType(DataType.Date)]
        public DateTime FechaEmision { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "El monto total es obligatorio.")]
        [DisplayName("Monto Total")]
        [Column(TypeName = "decimal(18, 2)")] // Para valores monetarios
        [DataType(DataType.Currency)]
        public decimal MontoTotal { get; set; }

        [StringLength(50)]
        [DisplayName("Estado")] // Ej: Pendiente, Pagada, Anulada
        public string Estado { get; set; } = "Pendiente";

        // Relación de Navegación
        public virtual ICollection<DetalleFactura>? DetallesFactura { get; set; }
    }
}