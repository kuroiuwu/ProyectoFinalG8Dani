using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class Mascota
    {
        [Key]
        public int IdMascota { get; set; }

        [Required(ErrorMessage = "El nombre de la mascota es obligatorio.")]
        [StringLength(100)]
        [DisplayName("Nombre Mascota")]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "La especie es obligatoria.")]
        [StringLength(50)]
        public string Especie { get; set; } = null!; // Ej: Perro, Gato

        [StringLength(50)]
        public string? Raza { get; set; }

        [DisplayName("Fecha de Nacimiento")]
        [DataType(DataType.Date)]
        public DateTime? FechaNacimiento { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un dueño.")]
        [DisplayName("Dueño")]
        public int IdUsuarioDueño { get; set; } // Clave foránea para el dueño

        [ForeignKey("IdUsuarioDueño")]
        public virtual Usuario? Dueño { get; set; }

        // Relaciones de Navegación
        public virtual ICollection<Cita>? Citas { get; set; }
        public virtual ICollection<HistorialMedico>? HistorialesMedicos { get; set; }
    }
}