using System.ComponentModel;

namespace ProyectoFinal_G8.ViewModels
{
    public class RolViewModel
    {
        public int Id { get; set; }

        [DisplayName("Nombre del Rol")]
        public string Name { get; set; } = null!;

        [DisplayName("Descripción")]
        public string? Descripcion { get; set; }
    }
}