using Microsoft.AspNetCore.Identity; 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    
    public class Usuario : IdentityUser<int>
    {
        
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [DisplayName("Nombre Completo")]
        
        public string Nombre { get; set; } = null!;

        
        [Required(ErrorMessage = "El rol es obligatorio.")]
        [DisplayName("Rol")]
        public int IdRol { get; set; }

        [ForeignKey("IdRol")]
        public virtual Rol? Rol { get; set; } 

        [DisplayName("Dirección")]
        [StringLength(200)]
        public string? Direccion { get; set; } 

        
        public virtual ICollection<Mascota>? Mascotas { get; set; }
        public virtual ICollection<Factura>? Facturas { get; set; }

        
        public virtual ICollection<Cita>? CitasComoCliente { get; set; }
        public virtual ICollection<Cita>? CitasComoVeterinario { get; set; }

        
    }
}