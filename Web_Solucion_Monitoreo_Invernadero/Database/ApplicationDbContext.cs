using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Database
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Planta> Plantas { get; set; }
        public DbSet<Invernadero> Invernaderos { get; set; }
        public DbSet<Sensor> Sensores { get; set; }
        public DbSet<RegistroHistorico> RegistroHistorico { get; set; }
        public DbSet<BitacoraRiegoAlerta> BitacoraRiegoAlertas { get; set; }
        public DbSet<CalendarioRiego> CalendarioRiego { get; set; }
        public DbSet<ComandoRiego> ComandosRiego { get; set; }
        public DbSet<AuditoriaCambio> AuditoriaCambios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Nombre)
                .IsUnique();

            modelBuilder.Entity<Usuario>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Relación estricta 1:1 -> Cada planta pertenece como máximo a un único invernadero
            modelBuilder.Entity<Invernadero>()
                .HasOne(i => i.Planta)
                .WithOne(p => p.Invernadero)
                .HasForeignKey<Invernadero>(i => i.IdPlanta)
                .OnDelete(DeleteBehavior.Restrict);

            // Evita ciclos de borrado en cascada con la tabla Usuarios
            modelBuilder.Entity<Invernadero>()
                .HasOne(i => i.UsuarioModificacion)
                .WithMany(u => u.InvernaderosModificados)
                .HasForeignKey(i => i.IdUsuarioModificacion)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CalendarioRiego>()
                .HasOne(c => c.Usuario)
                .WithMany(u => u.CalendariosCreados)
                .HasForeignKey(c => c.IdUsuario)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComandoRiego>()
                .HasIndex(c => c.ClaveIdempotencia)
                .IsUnique();

            modelBuilder.Entity<ComandoRiego>()
                .HasIndex(c => new { c.IdInvernadero, c.Estado, c.FechaExpiracion });

            modelBuilder.Entity<ComandoRiego>()
                .HasOne(c => c.Invernadero)
                .WithMany()
                .HasForeignKey(c => c.IdInvernadero)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ComandoRiego>()
                .HasOne(c => c.CalendarioRiego)
                .WithMany()
                .HasForeignKey(c => c.IdCalendarioRiego)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
