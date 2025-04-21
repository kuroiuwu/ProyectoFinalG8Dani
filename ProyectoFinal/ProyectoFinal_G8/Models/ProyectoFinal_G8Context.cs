using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // <-- Cambiar using
using Microsoft.AspNetCore.Identity; // <-- Añadir using
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal_G8.Models
{
    // Cambiar herencia a IdentityDbContext especificando Usuario, Rol y tipo de PK (int)
    public class ProyectoFinal_G8Context : IdentityDbContext<Usuario, Rol, int>
    {
        public ProyectoFinal_G8Context(DbContextOptions<ProyectoFinal_G8Context> options)
            : base(options)
        {
        }

        // ELIMINAR: DbSets para Usuario y Rol (ya incluidos en IdentityDbContext)
        // public DbSet<Usuario> Usuarios { get; set; } = default!;
        // public DbSet<Rol> Rols { get; set; } = default!;

        // Mantener DbSets para otras entidades
        public DbSet<Mascota> Mascotas { get; set; } = default!;
        public DbSet<Cita> Citas { get; set; } = default!;
        public DbSet<HistorialMedico> HistorialMedicos { get; set; } = default!;
        public DbSet<Factura> Facturas { get; set; } = default!;
        public DbSet<DetalleFactura> DetalleFacturas { get; set; } = default!;
        public DbSet<Insumo> Insumos { get; set; } = default!;
        public DbSet<Tratamiento> Tratamientos { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // MUY IMPORTANTE: Llamar a base.OnModelCreating PRIMERO para que Identity configure sus tablas
            base.OnModelCreating(modelBuilder);

            // --- Opcional: Renombrar tablas de Identity (Recomendado) ---
            modelBuilder.Entity<Usuario>(b => { b.ToTable("Usuarios"); });
            modelBuilder.Entity<Rol>(b => { b.ToTable("Roles"); });
            modelBuilder.Entity<IdentityUserRole<int>>(b => { b.ToTable("UsuarioRoles"); });
            modelBuilder.Entity<IdentityUserClaim<int>>(b => { b.ToTable("UsuarioClaims"); });
            modelBuilder.Entity<IdentityUserLogin<int>>(b => { b.ToTable("UsuarioLogins"); });
            modelBuilder.Entity<IdentityRoleClaim<int>>(b => { b.ToTable("RolClaims"); });
            modelBuilder.Entity<IdentityUserToken<int>>(b => { b.ToTable("UsuarioTokens"); });

            // --- Configuraciones de relaciones existentes (Revisar y Ajustar) ---

            // Configurar relación Usuario -> Rol (Uno a Muchos) - USANDO IdRol en Usuario
            // Si mantienes IdRol en Usuario como FK directa, esta configuración sigue siendo necesaria.
            // Si decides usar solo los roles de Identity (multi-rol), esta configuración se elimina.
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol) // Navegación desde Usuario
                .WithMany(r => r.Usuarios) // Navegación desde Rol
                .HasForeignKey(u => u.IdRol) // La FK definida en Usuario
                .OnDelete(DeleteBehavior.Restrict); // O tu regla de negocio

            // Configurar relación Usuario (Dueño) -> Mascota (Uno a Muchos)
            // Ahora usa la PK 'Id' de Usuario (IdentityUser) implícitamente
            modelBuilder.Entity<Mascota>()
                .HasOne(m => m.Dueño) // Asume que Mascota tiene una propiedad 'Dueño' de tipo Usuario
                .WithMany(u => u.Mascotas) // Colección en Usuario
                .HasForeignKey(m => m.IdUsuarioDueño) // FK en Mascota
                .OnDelete(DeleteBehavior.Cascade); // O tu regla de negocio

            // Configurar relación Mascota -> Cita (Uno a Muchos) - Sin cambios
            modelBuilder.Entity<Cita>()
                .HasOne(c => c.Mascota)
                .WithMany(m => m.Citas)
                .HasForeignKey(c => c.IdMascota)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relación Usuario (Veterinario) -> Cita (Uno a Muchos)
            // Ahora usa la PK 'Id' de Usuario (IdentityUser) implícitamente
            modelBuilder.Entity<Cita>()
                .HasOne(c => c.Veterinario) // Asume que Cita tiene una propiedad 'Veterinario' de tipo Usuario
                .WithMany(u => u.CitasComoVeterinario) // Colección específica en Usuario
                .HasForeignKey(c => c.IdUsuarioVeterinario) // FK en Cita
                .OnDelete(DeleteBehavior.Restrict);

            // Configurar relación Mascota -> HistorialMedico (Uno a Muchos) - Sin cambios
            modelBuilder.Entity<HistorialMedico>()
                .HasOne(h => h.Mascota)
                .WithMany(m => m.HistorialesMedicos)
                .HasForeignKey(h => h.IdMascota)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relación Usuario (Cliente) -> Factura (Uno a Muchos)
            // Ahora usa la PK 'Id' de Usuario (IdentityUser) implícitamente
            modelBuilder.Entity<Factura>()
                .HasOne(f => f.Cliente) // Asume que Factura tiene una propiedad 'Cliente' de tipo Usuario
                .WithMany(u => u.Facturas) // Colección en Usuario
                .HasForeignKey(f => f.IdUsuarioCliente) // FK en Factura
                .OnDelete(DeleteBehavior.Restrict);

            // Configurar relación Factura -> DetalleFactura (Uno a Muchos) - Sin cambios
            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Factura)
                .WithMany(f => f.DetallesFactura)
                .HasForeignKey(d => d.IdFactura)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurar relación DetalleFactura -> Insumo (Muchos a Uno) - Sin cambios
            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Insumo)
                .WithMany(i => i.DetallesFactura)
                .HasForeignKey(d => d.IdInsumo)
                .OnDelete(DeleteBehavior.Restrict);

            // Configurar relación DetalleFactura -> Tratamiento (Muchos a Uno) - Sin cambios
            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Tratamiento)
                .WithMany(t => t.DetallesFactura)
                .HasForeignKey(d => d.IdTratamiento)
                .OnDelete(DeleteBehavior.Restrict);

            // ELIMINAR O COMENTAR: Índice único en 'Correo'. Identity lo maneja para 'NormalizedEmail'.
            // Si intentas añadirlo de nuevo podría causar conflicto.
            // modelBuilder.Entity<Usuario>()
            //    .HasIndex(u => u.Correo) // Ahora es 'Email'
            //    .IsUnique();
        }
    }
}