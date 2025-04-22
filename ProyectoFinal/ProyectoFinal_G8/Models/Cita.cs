using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic; 

namespace ProyectoFinal_G8.Models
{
    // Clase estática para centralizar los nombres de los estados
    public static class EstadoCita
    {
        public const string Programada = "Programada";
        public const string Confirmada = "Confirmada";
        public const string Realizada = "Realizada";
        public const string CanceladaCliente = "Cancelada por Cliente";
        public const string CanceladaStaff = "Cancelada por Staff"; // O solo "Cancelada" si el staff la cancela/elimina
        public const string NoAsistio = "No Asistió";


        // Helper para obtener una lista de estados para dropdowns (útil para Admins/Vets)
        public static List<string> GetEstadosEditables()
        {
            return new List<string>
             {
                 Programada,
                 Confirmada,
                 Realizada,
                 CanceladaStaff, // El cliente usa su propia acción para cancelar
                 NoAsistio
             };
        }
    }


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
        public int IdUsuarioVeterinario { get; set; }

        [ForeignKey("IdUsuarioVeterinario")]
        public virtual Usuario? Veterinario { get; set; }

        [Required(ErrorMessage = "Debe seleccionar el tipo de cita.")]
        [Display(Name = "Tipo de Cita")]
        public int IdTipoCita { get; set; }

        [ForeignKey("IdTipoCita")]
        public virtual TipoCita? TipoCita { get; set; }

        [StringLength(50)]
        [DisplayName("Estado")]
        // Estado por defecto asignado en el constructor o en el controller al crear
        public string? Estado { get; set; } = EstadoCita.Programada; // Establecer estado inicial

        [StringLength(500)]
        [DisplayName("Notas Adicionales")] // Cambiado para claridad
        [DataType(DataType.MultilineText)]
        public string? Notas { get; set; }

        // Constructor para asegurar estado inicial si no se setea explícitamente
        public Cita()
        {
            Estado ??= EstadoCita.Programada;
        }
    }
}