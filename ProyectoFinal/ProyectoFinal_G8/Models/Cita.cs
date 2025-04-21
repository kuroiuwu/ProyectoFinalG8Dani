using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class Cita
    {
        [Key]
        public int IdCita { get; set; }

        [Required(ErrorMessage = "La fecha y hora de la cita son obligatorias.")]
        [DisplayName("Fecha y Hora")]
        [DataType(DataType.DateTime)]
        public DateTime FechaHora { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una mascota.")]
        [DisplayName("Mascota")]
        public int IdMascota { get; set; }

        [ForeignKey("IdMascota")]
        public virtual Mascota? Mascota { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un veterinario.")]
        [DisplayName("Veterinario")]
        public int IdUsuarioVeterinario { get; set; } // Asumiendo que los veterinarios son Usuarios

        [ForeignKey("IdUsuarioVeterinario")]
        public virtual Usuario? Veterinario { get; set; }

        // Podríamos tener una FK a Usuario Cliente también si es diferente al dueño de la mascota
        // [DisplayName("Cliente (si es diferente al dueño)")]
        // public int? IdUsuarioCliente { get; set; }
        // [ForeignKey("IdUsuarioCliente")]
        // public virtual Usuario Cliente { get; set; }

        [Required(ErrorMessage = "El motivo de la cita es obligatorio.")]
        [StringLength(250)]
        [DisplayName("Motivo")]
        public string Motivo { get; set; } = null!;

        [StringLength(50)]
        [DisplayName("Estado")] // Ej: Programada, Completada, Cancelada
        public string Estado { get; set; } = "Programada";

        [StringLength(500)]
        [DisplayName("Notas")]
        public string? Notas { get; set; } // Notas adicionales del veterinario o admin
    }
}