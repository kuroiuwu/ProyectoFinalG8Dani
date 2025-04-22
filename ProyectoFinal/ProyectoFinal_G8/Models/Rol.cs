using Microsoft.AspNetCore.Identity; 
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ProyectoFinal_G8.Models
{
    
    public class Rol : IdentityRole<int>
    {
       
        [StringLength(200)]
        [DisplayName("Descripción")]
        public string? Descripcion { get; set; }

        
        public virtual ICollection<Usuario>? Usuarios { get; set; }

        
    }
}