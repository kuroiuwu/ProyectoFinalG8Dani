using Microsoft.AspNetCore.Identity; // <-- Necesario
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoFinal_G8.Models
{
    // Heredar de IdentityUser<int> ya que tu PK original (IdUsuario) era int
    public class Usuario : IdentityUser<int>
    {
        // [Key] // La Key 'Id' es heredada de IdentityUser<int>
        // public int IdUsuario { get; set; }

        // Propiedades personalizadas que quieres mantener:
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [StringLength(100)]
        [DisplayName("Nombre Completo")]
        // Nota: IdentityUser tiene UserName. Puedes decidir si 'Nombre' es algo
        // diferente o si quieres mapear/sincronizar esto con UserName o alguna Claim.
        // Por ahora, la dejamos como una propiedad adicional.
        public string Nombre { get; set; } = null!;

        // 'Correo' se mapea a la propiedad 'Email' heredada de IdentityUser
        // Los atributos [Required], [EmailAddress], [StringLength] se aplicarán
        // a la propiedad 'Email' a través de la configuración de Identity o DataAnnotations si es necesario.
        // [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        // [EmailAddress(ErrorMessage = "El formato del correo no es válido.")]
        // [StringLength(100)]
        // [DisplayName("Correo Electrónico")]
        // public string Correo { get; set; } = null!; // Usa la propiedad 'Email' heredada

        // ELIMINAR: La contraseña es manejada por Identity (PasswordHash heredado)
        // [Required(ErrorMessage = "La contraseña es obligatoria.")]
        // [StringLength(255)]
        // [DataType(DataType.Password)]
        // public string Contraseña { get; set; } = null!;

        // Clave foránea para el Rol "principal" (si mantienes este enfoque)
        [Required(ErrorMessage = "El rol es obligatorio.")]
        [DisplayName("Rol")]
        public int IdRol { get; set; }

        [ForeignKey("IdRol")]
        public virtual Rol? Rol { get; set; } // Navegación al rol principal

        // 'Telefono' puede mapearse a 'PhoneNumber' heredado de IdentityUser
        // O mantenerse como propiedad separada si necesitas algo diferente.
        // Vamos a mapearlo para usar las características de Identity:
        // [DisplayName("Teléfono")]
        // [StringLength(20)]
        // public string? Telefono { get; set; } // Usa la propiedad 'PhoneNumber' heredada

        [DisplayName("Dirección")]
        [StringLength(200)]
        public string? Direccion { get; set; } // Propiedad personalizada

        // --- Relaciones de Navegación (Se mantienen) ---
        public virtual ICollection<Mascota>? Mascotas { get; set; }
        public virtual ICollection<Factura>? Facturas { get; set; }

        // Renombrar para claridad si usas InverseProperty en Cita
        public virtual ICollection<Cita>? CitasComoCliente { get; set; }
        public virtual ICollection<Cita>? CitasComoVeterinario { get; set; }

        // --- Notas Adicionales ---
        // 1. Asegúrate de asignar un valor a 'UserName' (heredado) al crear un usuario.
        //    Comúnmente, se usa el mismo valor que 'Email'.
        // 2. La propiedad 'PhoneNumber' (heredada) puede usarse en lugar de 'Telefono'.
        // 3. La propiedad 'Email' (heredada) reemplaza a 'Correo'.
    }
}