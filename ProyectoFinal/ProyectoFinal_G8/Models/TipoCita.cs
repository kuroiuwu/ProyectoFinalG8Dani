// Models/TipoCita.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;

namespace ProyectoFinal_G8.Models
{
    public class TipoCita
    {
        [Key]
        public int IdTipoCita { get; set; }

        [Required(ErrorMessage = "El nombre del tipo de cita es obligatorio.")]
        [StringLength(100)]
        [DisplayName("Tipo de Cita")]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "La duración es obligatoria.")]
        [Range(15, 240, ErrorMessage = "La duración debe estar entre 15 y 240 minutos.")] // Rango ejemplo
        [DisplayName("Duración (Minutos)")]
        public int DuracionMinutos { get; set; }

        // Propiedad de navegación inversa (opcional, pero útil)
        public virtual ICollection<Cita> Citas { get; set; } = new List<Cita>();
    }
}