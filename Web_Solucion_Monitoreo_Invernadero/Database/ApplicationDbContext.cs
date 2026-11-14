using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Database;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BitacoraRiegoAlerta> BitacoraRiegoAlertas { get; set; }

    public virtual DbSet<CalendarioRiego> CalendarioRiegos { get; set; }

    public virtual DbSet<Invernadero> Invernaderos { get; set; }

    public virtual DbSet<Planta> Plantas { get; set; }

    public virtual DbSet<RegistroHistorico> RegistroHistoricos { get; set; }

    public virtual DbSet<Sensore> Sensores { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql("server=localhost;port=3306;database=monitoreo_orquideas;uid=root", Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.4.3-mysql"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<BitacoraRiegoAlerta>(entity =>
        {
            entity.HasKey(e => e.IdEvento).HasName("PRIMARY");

            entity.ToTable("bitacora_riego_alertas");

            entity.HasIndex(e => new { e.IdInvernadero, e.FechaHora }, "idx_bitacora_inv_fecha");

            entity.Property(e => e.IdEvento).HasColumnName("id_evento");
            entity.Property(e => e.Descripcion)
                .HasMaxLength(255)
                .HasColumnName("descripcion");
            entity.Property(e => e.FechaHora)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("fecha_hora");
            entity.Property(e => e.HumedadMomento)
                .HasPrecision(5, 2)
                .HasColumnName("humedad_momento");
            entity.Property(e => e.IdInvernadero).HasColumnName("id_invernadero");
            entity.Property(e => e.Leida).HasColumnName("leida");
            entity.Property(e => e.TemperaturaMomento)
                .HasPrecision(5, 2)
                .HasColumnName("temperatura_momento");
            entity.Property(e => e.TiempoActivacionSeg).HasColumnName("tiempo_activacion_seg");
            entity.Property(e => e.TipoEvento)
                .HasColumnType("enum('RIEGO_PROGRAMADO','RIEGO_EMERGENCIA','RIEGO_POSPUESTO','ALERTA_UMBRAL','ERROR_SENSOR')")
                .HasColumnName("tipo_evento");

            entity.HasOne(d => d.IdInvernaderoNavigation).WithMany(p => p.BitacoraRiegoAlerta)
                .HasForeignKey(d => d.IdInvernadero)
                .HasConstraintName("fk_bitacora_invernadero");
        });

        modelBuilder.Entity<CalendarioRiego>(entity =>
        {
            entity.HasKey(e => e.IdCalendarioRiego).HasName("PRIMARY");

            entity.ToTable("calendario_riego");

            entity.HasIndex(e => e.IdPlanta, "fk_calendario_planta");

            entity.Property(e => e.IdCalendarioRiego).HasColumnName("id_calendario_riego");
            entity.Property(e => e.DiaSemana)
                .HasMaxLength(15)
                .HasColumnName("dia_semana");
            entity.Property(e => e.FechaEspecifica).HasColumnName("fecha_especifica");
            entity.Property(e => e.HoraRiego)
                .HasColumnType("time")
                .HasColumnName("hora_riego");
            entity.Property(e => e.IdPlanta).HasColumnName("id_planta");
            entity.Property(e => e.PospuestoHasta).HasColumnName("pospuesto_hasta");
            entity.Property(e => e.State)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("state");
            entity.Property(e => e.TiempoRiegoSeg)
                .HasDefaultValueSql("'15'")
                .HasColumnName("tiempo_riego_seg");

            entity.HasOne(d => d.IdPlantaNavigation).WithMany(p => p.CalendarioRiegos)
                .HasForeignKey(d => d.IdPlanta)
                .HasConstraintName("fk_calendario_planta");
        });

        modelBuilder.Entity<Invernadero>(entity =>
        {
            entity.HasKey(e => e.IdInvernadero).HasName("PRIMARY");

            entity.ToTable("invernaderos");

            entity.HasIndex(e => e.IdUsuario, "fk_invernadero_usuario");

            entity.HasIndex(e => e.IdPlanta, "id_planta").IsUnique();

            entity.Property(e => e.IdInvernadero).HasColumnName("id_invernadero");
            entity.Property(e => e.FrecuenciaRegistroMin)
                .HasDefaultValueSql("'60'")
                .HasColumnName("frecuencia_registro_min");
            entity.Property(e => e.IdPlanta).HasColumnName("id_planta");
            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.State)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("state");
            entity.Property(e => e.Ubicacion)
                .HasMaxLength(150)
                .HasColumnName("ubicacion");

            entity.HasOne(d => d.IdPlantaNavigation).WithOne(p => p.Invernadero)
                .HasForeignKey<Invernadero>(d => d.IdPlanta)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_invernadero_planta");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Invernaderos)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_invernadero_usuario");
        });

        modelBuilder.Entity<Planta>(entity =>
        {
            entity.HasKey(e => e.IdPlanta).HasName("PRIMARY");

            entity.ToTable("plantas");

            entity.Property(e => e.IdPlanta).HasColumnName("id_planta");
            entity.Property(e => e.HumedadMaxima)
                .HasPrecision(5, 2)
                .HasColumnName("humedad_maxima");
            entity.Property(e => e.HumedadMinima)
                .HasPrecision(5, 2)
                .HasColumnName("humedad_minima");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
            entity.Property(e => e.State)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("state");
            entity.Property(e => e.TemperaturaMaxima)
                .HasPrecision(5, 2)
                .HasColumnName("temperatura_maxima");
            entity.Property(e => e.TemperaturaMinima)
                .HasPrecision(5, 2)
                .HasColumnName("temperatura_minima");
        });

        modelBuilder.Entity<RegistroHistorico>(entity =>
        {
            entity.HasKey(e => e.IdRegistro).HasName("PRIMARY");

            entity.ToTable("registro_historico");

            entity.HasIndex(e => new { e.IdInvernadero, e.FechaHora }, "idx_historico_inv_fecha");

            entity.Property(e => e.IdRegistro).HasColumnName("id_registro");
            entity.Property(e => e.FechaHora)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime")
                .HasColumnName("fecha_hora");
            entity.Property(e => e.Humedad)
                .HasPrecision(5, 2)
                .HasColumnName("humedad");
            entity.Property(e => e.IdInvernadero).HasColumnName("id_invernadero");
            entity.Property(e => e.State)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("state");
            entity.Property(e => e.Temperatura)
                .HasPrecision(5, 2)
                .HasColumnName("temperatura");

            entity.HasOne(d => d.IdInvernaderoNavigation).WithMany(p => p.RegistroHistoricos)
                .HasForeignKey(d => d.IdInvernadero)
                .HasConstraintName("fk_historico_invernadero");
        });

        modelBuilder.Entity<Sensore>(entity =>
        {
            entity.HasKey(e => e.IdSensor).HasName("PRIMARY");

            entity.ToTable("sensores");

            entity.HasIndex(e => e.CodigoDispositivo, "codigo_dispositivo").IsUnique();

            entity.HasIndex(e => e.IdInvernadero, "fk_sensor_invernadero");

            entity.Property(e => e.IdSensor).HasColumnName("id_sensor");
            entity.Property(e => e.CodigoDispositivo)
                .HasMaxLength(50)
                .HasColumnName("codigo_dispositivo");
            entity.Property(e => e.EstadoConexion)
                .HasDefaultValueSql("'Desconectado'")
                .HasColumnType("enum('Conectado','Desconectado')")
                .HasColumnName("estado_conexion");
            entity.Property(e => e.IdInvernadero).HasColumnName("id_invernadero");
            entity.Property(e => e.State)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("state");
            entity.Property(e => e.TipoConexion)
                .HasDefaultValueSql("'WiFi'")
                .HasColumnType("enum('WiFi','Bluetooth')")
                .HasColumnName("tipo_conexion");
            entity.Property(e => e.UltimaSenal)
                .HasColumnType("datetime")
                .HasColumnName("ultima_senal");

            entity.HasOne(d => d.IdInvernaderoNavigation).WithMany(p => p.Sensores)
                .HasForeignKey(d => d.IdInvernadero)
                .HasConstraintName("fk_sensor_invernadero");
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PRIMARY");

            entity.ToTable("usuarios");

            entity.HasIndex(e => e.Email, "email").IsUnique();

            entity.Property(e => e.IdUsuario).HasColumnName("id_usuario");
            entity.Property(e => e.Email)
                .HasMaxLength(120)
                .HasColumnName("email");
            entity.Property(e => e.Nombre)
                .HasMaxLength(100)
                .HasColumnName("nombre");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.State)
                .IsRequired()
                .HasDefaultValueSql("'1'")
                .HasColumnName("state");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
