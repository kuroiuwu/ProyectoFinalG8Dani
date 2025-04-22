using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ProyectoFinal_G8.Models; // Para EstadoCita y validaciones

namespace ProyectoFinal_G8.Models.ViewModels
{
    // ViewModel para combinar datos de Cita y potencialmente nueva Mascota
    public class CitaCreateViewModel : IValidatableObject // Implementa para validación condicional
    {
        // --- Propiedades de Cita ---
        [Required(ErrorMessage = "La fecha y hora de la cita son obligatorias.")]
        [DisplayName("Fecha y Hora")]
        [DataType(DataType.DateTime)]
        public DateTime FechaHora { get; set; }

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

        // --- Selección de Mascota ---
        [DisplayName("Mascota Existente")]
        // No es [Required] aquí, porque puede ser una mascota nueva
        public int? IdMascotaSeleccionada { get; set; }

        [Display(Name = "¿Registrar Mascota Nueva para esta Cita?")]
        public bool RegistrarNuevaMascota { get; set; } = false; // Por defecto, no

        // --- Propiedades para Nueva Mascota ---
        // Estas propiedades solo son relevantes si RegistrarNuevaMascota es true

        [Display(Name = "Nombre (Nueva Mascota)")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        // Required se valida condicionalmente abajo
        public string? NuevoNombreMascota { get; set; }

        [Display(Name = "Especie (Nueva Mascota)")]
        [StringLength(50)]
        // Required se valida condicionalmente abajo
        public string? NuevaEspecie { get; set; }

        [StringLength(50)]
        [Display(Name = "Raza (Nueva Mascota - Opcional)")]
        public string? NuevaRaza { get; set; }

        [DisplayName("Fecha de Nacimiento (Nueva Mascota - Opcional)")]
        [DataType(DataType.Date)]
        public DateTime? NuevaFechaNacimiento { get; set; }

        // --- Método de Validación Condicional ---
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Si NO se registra una nueva mascota, se DEBE seleccionar una existente.
            if (!RegistrarNuevaMascota && !IdMascotaSeleccionada.HasValue)
            {
                // Error aplicado a la propiedad del dropdown
                yield return new ValidationResult("Debe seleccionar una mascota existente o registrar una nueva.", new[] { nameof(IdMascotaSeleccionada) });
            }

            // Si SÍ se registra una nueva mascota, los campos requeridos para ella deben estar llenos.
            if (RegistrarNuevaMascota)
            {
                if (string.IsNullOrWhiteSpace(NuevoNombreMascota))
                {
                    yield return new ValidationResult("El nombre de la nueva mascota es obligatorio.", new[] { nameof(NuevoNombreMascota) });
                }
                if (string.IsNullOrWhiteSpace(NuevaEspecie))
                {
                    yield return new ValidationResult("La especie de la nueva mascota es obligatoria.", new[] { nameof(NuevaEspecie) });
                }
                // Puedes añadir más validaciones aquí si es necesario (ej: FechaNacimiento no futura)
                if (NuevaFechaNacimiento.HasValue && NuevaFechaNacimiento.Value.Date > DateTime.Today)
                {
                    yield return new ValidationResult("La fecha de nacimiento no puede ser futura.", new[] { nameof(NuevaFechaNacimiento) });
                }
            }
        }
    }
}