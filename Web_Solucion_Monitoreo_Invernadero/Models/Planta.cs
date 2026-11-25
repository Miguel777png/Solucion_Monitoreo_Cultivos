using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("plantas")]
    public class Planta
    {
        [Key]
        [Column("id_planta")]
        public int IdPlanta { get; set; }

        [Required(ErrorMessage = "El nombre de la planta es obligatorio")]
        [StringLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(255)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Required]
        [Column("temperatura_minima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMinima { get; set; }

        [Required]
        [Column("temperatura_maxima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMaxima { get; set; }

        [Required]
        [Column("temperatura_optima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaOptima { get; set; }

        [Required]
        [Column("humedad_minima", TypeName = "decimal(5,2)")]
        public decimal HumedadMinima { get; set; }

        [Required]
        [Column("humedad_maxima", TypeName = "decimal(5,2)")]
        public decimal HumedadMaxima { get; set; }

        [Required]
        [Column("humedad_optima", TypeName = "decimal(5,2)")]
        public decimal HumedadOptima { get; set; }

        [Required]
        [Column("id_usuario_creacion")]
        public int IdUsuarioCreacion { get; set; }

        [Column("fecha_actualizacion")]
        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        [Column("state")]
        public bool State { get; set; } = true;

        [ForeignKey(nameof(IdUsuarioCreacion))]
        public virtual Usuario? UsuarioCreacion { get; set; }

        // Una especie del catálogo puede estar asignada a varios invernaderos
        public virtual Invernadero? Invernadero { get; set; }
    }
}
