using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class HistorialMedico
    {
        [Key]
        public int IdHistorial { get; set; }

        [Required(ErrorMessage = "Debe asociar el historial a una mascota.")]
        [DisplayName("Mascota")]
        public int IdMascota { get; set; }

        [ForeignKey("IdMascota")]
        public virtual Mascota? Mascota { get; set; }

        [Required(ErrorMessage = "La fecha del registro es obligatoria.")]
        [DisplayName("Fecha Registro")]
        [DataType(DataType.Date)]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        [DisplayName("Descripción / Diagnóstico")]
        public string Descripcion { get; set; } = null!;

        [DisplayName("Tratamiento Aplicado")]
        public string? Tratamiento { get; set; } // Podría ser una FK a una tabla Tratamientos si es complejo

        [DisplayName("Notas Adicionales")]
        public string? Notas { get; set; }

        // Opcional: Relacionar con el veterinario que hizo el registro
        // [DisplayName("Veterinario")]
        // public int? IdUsuarioVeterinario { get; set; }
        // [ForeignKey("IdUsuarioVeterinario")]
        // public virtual Usuario Veterinario { get; set; }
    }
}