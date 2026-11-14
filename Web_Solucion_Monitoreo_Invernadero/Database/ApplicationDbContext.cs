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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Garantiza la relación estricta 1:1 entre Invernadero y Planta
            modelBuilder.Entity<Invernadero>()
                .HasOne(i => i.Planta)
                .WithOne(p => p.Invernadero)
                .HasForeignKey<Invernadero>(i => i.IdPlanta);
        }
    }
}