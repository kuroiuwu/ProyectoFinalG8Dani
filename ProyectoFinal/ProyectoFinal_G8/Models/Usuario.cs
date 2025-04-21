using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    public class Usuario
    {
        [Key]
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [DisplayName("Nombre Completo")]
        public string Nombre { get; set; } = null!;

        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        [StringLength(100)]
        [DisplayName("Correo Electrónico")]
        public string Correo { get; set; } = null!;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(255)] 
        [DataType(DataType.Password)]
        public string Contraseña { get; set; } = null!; 

        [Required(ErrorMessage = "El rol es obligatorio.")]
        [DisplayName("Rol")]
        public int IdRol { get; set; }

        [ForeignKey("IdRol")]
        public virtual Rol? Rol { get; set; }

        [DisplayName("Teléfono")]
        [StringLength(20)]
        public string? Telefono { get; set; }

        [DisplayName("Dirección")]
        [StringLength(200)]
        public string? Direccion { get; set; }

        // Relaciones de Navegación
        // Un usuario (cliente) puede tener muchas mascotas
        public virtual ICollection<Mascota>? Mascotas { get; set; }

        // Un usuario (cliente) puede tener muchas facturas
        public virtual ICollection<Factura>? Facturas { get; set; }

        // Un usuario (cliente o veterinario) puede tener muchas citas
        public virtual ICollection<Cita>? CitasComoCliente { get; set; }
        public virtual ICollection<Cita>? CitasComoVeterinario { get; set; }
    }
}