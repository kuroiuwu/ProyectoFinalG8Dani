using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ProyectoFinal_G8.Models; // Asegúrate que esto apunte a tus modelos base

namespace ProyectoFinal_G8.Models.ViewModels
{
    public class CitaEditViewModel
    {
        public int IdCita { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una fecha para la cita.")]
        [DisplayName("Fecha de la Cita")]
        [DataType(DataType.Date)]
        public DateTime SelectedDate { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una hora disponible.")]
        [DisplayName("Hora Seleccionada")]
        public string SelectedTime { get; set; } = string.Empty; // Ej: "09:00"

        [Required(ErrorMessage = "Debe seleccionar una mascota.")]
        [DisplayName("Mascota")]
        public int IdMascota { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un veterinario.")]
        [DisplayName("Veterinario")]
        public int IdUsuarioVeterinario { get; set; }

        [Required(ErrorMessage = "Debe seleccionar el tipo de cita.")]
        [Display(Name = "Tipo de Cita")]
        public int IdTipoCita { get; set; }

        [Required(ErrorMessage = "El estado de la cita es obligatorio.")]
        [StringLength(50)]
        [DisplayName("Estado")]
        public string? Estado { get; set; } // Admins pueden cambiar estado

        [StringLength(500)]
        [DisplayName("Notas Adicionales")]
        [DataType(DataType.MultilineText)]
        public string? Notas { get; set; }

        // Propiedades solo para mostrar (opcional, podrías obtenerlas en la vista si prefieres)
        [ReadOnly(true)]
        public string? MascotaNombre { get; set; }
        [ReadOnly(true)]
        public string? DuenoNombre { get; set; }
    }
}