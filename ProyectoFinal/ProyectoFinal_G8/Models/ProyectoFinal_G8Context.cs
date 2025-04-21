using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal_G8.Models
{
    public class ProyectoFinal_G8Context : DbContext
    {
        public ProyectoFinal_G8Context(DbContextOptions<ProyectoFinal_G8Context> options)
            : base(options)
        {
        }

        // DbSets para todas tus entidades
        public DbSet<Usuario> Usuarios { get; set; } = default!;
        public DbSet<Rol> Rols { get; set; } = default!;
        public DbSet<Mascota> Mascotas { get; set; } = default!;
        public DbSet<Cita> Citas { get; set; } = default!; // Añadido
        public DbSet<HistorialMedico> HistorialMedicos { get; set; } = default!;
        public DbSet<Factura> Facturas { get; set; } = default!;
        public DbSet<DetalleFactura> DetalleFacturas { get; set; } = default!;
        public DbSet<Insumo> Insumos { get; set; } = default!; // Añadido
        public DbSet<Tratamiento> Tratamientos { get; set; } = default!; // Añadido

        // Configuración adicional con Fluent API (Opcional pero recomendado para relaciones complejas)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configurar relación Usuario -> Rol (Uno a Muchos)
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.IdRol)
                .OnDelete(DeleteBehavior.Restrict); // O .SetNull, o .Cascade según reglas de negocio

            // Configurar relación Usuario (Dueño) -> Mascota (Uno a Muchos)
            modelBuilder.Entity<Mascota>()
                .HasOne(m => m.Dueño)
                .WithMany(u => u.Mascotas)
                .HasForeignKey(m => m.IdUsuarioDueño)
                .OnDelete(DeleteBehavior.Cascade); // Si se borra el dueño, ¿se borran sus mascotas? O Restrict

            // Configurar relación Mascota -> Cita (Uno a Muchos)
            modelBuilder.Entity<Cita>()
                .HasOne(c => c.Mascota)
                .WithMany(m => m.Citas)
                .HasForeignKey(c => c.IdMascota)
                .OnDelete(DeleteBehavior.Cascade); // Si se borra la mascota, ¿se borran sus citas?

            // Configurar relación Usuario (Veterinario) -> Cita (Uno a Muchos)
            modelBuilder.Entity<Cita>()
                .HasOne(c => c.Veterinario)
                .WithMany(u => u.CitasComoVeterinario) // Usar la colección específica
                .HasForeignKey(c => c.IdUsuarioVeterinario)
                .OnDelete(DeleteBehavior.Restrict); // No borrar veterinario si tiene citas

            // Configurar relación Mascota -> HistorialMedico (Uno a Muchos)
            modelBuilder.Entity<HistorialMedico>()
                .HasOne(h => h.Mascota)
                .WithMany(m => m.HistorialesMedicos)
                .HasForeignKey(h => h.IdMascota)
                .OnDelete(DeleteBehavior.Cascade); // Si se borra la mascota, borrar su historial

            // Configurar relación Usuario (Cliente) -> Factura (Uno a Muchos)
            modelBuilder.Entity<Factura>()
                .HasOne(f => f.Cliente)
                .WithMany(u => u.Facturas)
                .HasForeignKey(f => f.IdUsuarioCliente)
                .OnDelete(DeleteBehavior.Restrict); // No borrar cliente si tiene facturas

            // Configurar relación Factura -> DetalleFactura (Uno a Muchos)
            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Factura)
                .WithMany(f => f.DetallesFactura)
                .HasForeignKey(d => d.IdFactura)
                .OnDelete(DeleteBehavior.Cascade); // Si se borra la factura, borrar sus detalles

            // Opcional: Configurar relación DetalleFactura -> Insumo (Muchos a Uno)
            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Insumo)
                .WithMany(i => i.DetallesFactura)
                .HasForeignKey(d => d.IdInsumo)
                .OnDelete(DeleteBehavior.Restrict); // No borrar insumo si está en una factura

            // Opcional: Configurar relación DetalleFactura -> Tratamiento (Muchos a Uno)
            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Tratamiento)
                .WithMany(t => t.DetallesFactura)
                .HasForeignKey(d => d.IdTratamiento)
                .OnDelete(DeleteBehavior.Restrict); // No borrar tratamiento si está en una factura

            // Definir que el correo del usuario debe ser único
            modelBuilder.Entity<Usuario>()
               .HasIndex(u => u.Correo)
               .IsUnique();
        }
    }
}