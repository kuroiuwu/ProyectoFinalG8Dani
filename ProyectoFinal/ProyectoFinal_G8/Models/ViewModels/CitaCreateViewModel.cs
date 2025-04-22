// CitaCreateViewModel.cs (Modificaciones)
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding; // Para BindNever
using ProyectoFinal_G8.Models;

namespace ProyectoFinal_G8.Models.ViewModels
{
    public class CitaCreateViewModel : IValidatableObject
    {
        // --- Propiedades para la Vista (NUEVAS) ---
        [Required(ErrorMessage = "Debe seleccionar una fecha para la cita.")]
        [DisplayName("Fecha de la Cita")]
        [DataType(DataType.Date)] // Importante para que el input type="date" funcione bien
        public DateTime SelectedDate { get; set; } = DateTime.Today.AddDays(1); // Default a mañana

        [Required(ErrorMessage = "Debe seleccionar una hora disponible.")]
        [DisplayName("Hora Seleccionada")]
        public string SelectedTime { get; set; } = string.Empty; // Ej: "09:00", "14:00"


        // --- Propiedad FechaHora (INTERNA) ---
        // La mantenemos para la lógica interna pero no la exponemos directamente en el form principal
        [BindNever] // Evita que se enlace directamente desde el form principal
        public DateTime FechaHora { get; set; }


        // --- Propiedades de Cita (Existentes) ---
        [Required(ErrorMessage = "Debe seleccionar un veterinario.")]
        [DisplayName("Veterinario")]
        public int IdUsuarioVeterinario { get; set; }

        [Required(ErrorMessage = "Debe seleccionar el tipo de cita.")]
        [Display(Name = "Tipo de Cita")]
        public int IdTipoCita { get; set; }

        [StringLength(500)]
        [DisplayName("Notas Adicionales")]
        [DataType(DataType.MultilineText)]
        public string? Notas { get; set; }

        // --- Selección de Mascota (Existente) ---
        [DisplayName("Mascota Existente")]
        public int? IdMascotaSeleccionada { get; set; }

        [Display(Name = "¿Registrar Mascota Nueva para esta Cita?")]
        public bool RegistrarNuevaMascota { get; set; } = false;

        // --- Propiedades para Nueva Mascota (Existentes) ---
        [Display(Name = "Nombre (Nueva Mascota)")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        public string? NuevoNombreMascota { get; set; }

        [Display(Name = "Especie (Nueva Mascota)")]
        [StringLength(50)]
        public string? NuevaEspecie { get; set; }

        [StringLength(50)]
        [Display(Name = "Raza (Nueva Mascota - Opcional)")]
        public string? NuevaRaza { get; set; }

        [DisplayName("Fecha de Nacimiento (Nueva Mascota - Opcional)")]
        [DataType(DataType.Date)]
        public DateTime? NuevaFechaNacimiento { get; set; }


        // --- Método de Validación Condicional (Adaptado) ---
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Validación de Mascota (igual que antes)
            if (!RegistrarNuevaMascota && !IdMascotaSeleccionada.HasValue)
            {
                yield return new ValidationResult("Debe seleccionar una mascota existente o registrar una nueva.", new[] { nameof(IdMascotaSeleccionada) });
            }
            if (RegistrarNuevaMascota)
            {
                if (string.IsNullOrWhiteSpace(NuevoNombreMascota))
                { yield return new ValidationResult("El nombre de la nueva mascota es obligatorio.", new[] { nameof(NuevoNombreMascota) }); }
                if (string.IsNullOrWhiteSpace(NuevaEspecie))
                { yield return new ValidationResult("La especie de la nueva mascota es obligatoria.", new[] { nameof(NuevaEspecie) }); }
                if (NuevaFechaNacimiento.HasValue && NuevaFechaNacimiento.Value.Date > DateTime.Today)
                { yield return new ValidationResult("La fecha de nacimiento no puede ser futura.", new[] { nameof(NuevaFechaNacimiento) }); }
            }

            // Validación de Fecha/Hora (Ahora es más simple aquí, la lógica compleja está en el Controller/JS)
            if (SelectedDate.Date < DateTime.Today) // Ya no puede ser hoy
            {
                yield return new ValidationResult("La fecha de la cita debe ser en el futuro.", new[] { nameof(SelectedDate) });
            }
            // La validación de SelectedTime (formato HH:mm y si es requerida) la manejan los DataAnnotations y el dropdown.
            // La validación de si la hora ES VÁLIDA para esa fecha se hará en el Controller POST.
        }
    }
}