using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ProyectoFinal_G8.Models
{
    public class ProyectoFinal_G8Context : IdentityDbContext<Usuario, Rol, int>
    {
        public ProyectoFinal_G8Context(DbContextOptions<ProyectoFinal_G8Context> options)
            : base(options)
        {
        }

        
        public DbSet<Mascota> Mascotas { get; set; } = default!;
        public DbSet<Cita> Citas { get; set; } = default!;
        public DbSet<HistorialMedico> HistorialMedicos { get; set; } = default!;
        public DbSet<Factura> Facturas { get; set; } = default!;
        public DbSet<DetalleFactura> DetalleFacturas { get; set; } = default!;
        public DbSet<Insumo> Insumos { get; set; } = default!;
        public DbSet<Tratamiento> Tratamientos { get; set; } = default!;

        // --- AÑADIR DbSet para TipoCita ---
        public DbSet<TipoCita> TiposCita { get; set; } = default!;
        // ----------------------------------

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Mantener al principio

            // --- Renombrar tablas Identity  ---
            modelBuilder.Entity<Usuario>(b => { b.ToTable("Usuarios"); });
            modelBuilder.Entity<Rol>(b => { b.ToTable("Roles"); });
            modelBuilder.Entity<IdentityUserRole<int>>(b => { b.ToTable("UsuarioRoles"); });
            modelBuilder.Entity<IdentityUserClaim<int>>(b => { b.ToTable("UsuarioClaims"); });
            modelBuilder.Entity<IdentityUserLogin<int>>(b => { b.ToTable("UsuarioLogins"); });
            modelBuilder.Entity<IdentityRoleClaim<int>>(b => { b.ToTable("RolClaims"); });
            modelBuilder.Entity<IdentityUserToken<int>>(b => { b.ToTable("UsuarioTokens"); });
            // ----------------------------------------------------

            
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.IdRol)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Mascota>()
                .HasOne(m => m.Dueño)
                .WithMany(u => u.Mascotas)
                .HasForeignKey(m => m.IdUsuarioDueño)
                .OnDelete(DeleteBehavior.Cascade); // O Restrict

            modelBuilder.Entity<HistorialMedico>()
                .HasOne(h => h.Mascota)
                .WithMany(m => m.HistorialesMedicos)
                .HasForeignKey(h => h.IdMascota)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Factura>()
                .HasOne(f => f.Cliente)
                .WithMany(u => u.Facturas)
                .HasForeignKey(f => f.IdUsuarioCliente)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetalleFactura>()
                .HasOne(d => d.Factura)
                .WithMany(f => f.DetallesFactura)
                .HasForeignKey(d => d.IdFactura)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<DetalleFactura>()
               .HasOne(d => d.Insumo)
               .WithMany(i => i.DetallesFactura)
               .HasForeignKey(d => d.IdInsumo)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DetalleFactura>()
               .HasOne(d => d.Tratamiento)
               .WithMany(t => t.DetallesFactura)
               .HasForeignKey(d => d.IdTratamiento)
               .OnDelete(DeleteBehavior.Restrict);
            // ---------------------------------------

           
            modelBuilder.Entity<Cita>(entity =>
            {
                // Relación con Mascota
                entity.HasOne(c => c.Mascota)
                    .WithMany(m => m.Citas)
                    .HasForeignKey(c => c.IdMascota)
                    .OnDelete(DeleteBehavior.Cascade); // O Restrict

                // Relación con Veterinario (Usuario)
                entity.HasOne(c => c.Veterinario)
                    .WithMany(u => u.CitasComoVeterinario)
                    .HasForeignKey(c => c.IdUsuarioVeterinario)
                    .OnDelete(DeleteBehavior.Restrict);

                // NUEVA Relación con TipoCita
                entity.HasOne(c => c.TipoCita)
                    .WithMany(t => t.Citas)
                    .HasForeignKey(c => c.IdTipoCita)
                    .OnDelete(DeleteBehavior.Restrict); // No borrar tipo si hay citas asociadas
            });
            // ---------------------------------------
        }
    }
}