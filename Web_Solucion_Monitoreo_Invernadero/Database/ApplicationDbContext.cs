using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Database
{
    /// <summary>
    /// Representa la conexión y las tablas usadas por Entity Framework.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Inicializa el contexto con la configuración registrada en la aplicación.
        /// </summary>
        /// <param name="options">Opciones de conexión y proveedor de base de datos.</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        /// <summary>Tabla de usuarios.</summary>
        public DbSet<Usuario> Usuarios { get; set; }
        /// <summary>Tabla del catálogo de plantas.</summary>
        public DbSet<Planta> Plantas { get; set; }
        /// <summary>Tabla de invernaderos.</summary>
        public DbSet<Invernadero> Invernaderos { get; set; }
        /// <summary>Tabla de sensores ESP32.</summary>
        public DbSet<Sensor> Sensores { get; set; }
        /// <summary>Tabla del historial ambiental.</summary>
        public DbSet<RegistroHistorico> RegistroHistorico { get; set; }
        /// <summary>Tabla de riegos y alertas.</summary>
        public DbSet<BitacoraRiegoAlerta> BitacoraRiegoAlertas { get; set; }
        /// <summary>Tabla de horarios de riego.</summary>
        public DbSet<CalendarioRiego> CalendarioRiego { get; set; }
        /// <summary>Tabla de órdenes de riego.</summary>
        public DbSet<ComandoRiego> ComandosRiego { get; set; }
        /// <summary>Tabla de auditoría.</summary>
        public DbSet<AuditoriaCambio> AuditoriaCambios { get; set; }

        /// <summary>
        /// Configura índices, relaciones y restricciones de las entidades.
        /// </summary>
        /// <param name="modelBuilder">Constructor del modelo de Entity Framework.</param>
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
