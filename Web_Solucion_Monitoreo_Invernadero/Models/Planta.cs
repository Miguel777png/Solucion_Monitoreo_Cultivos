using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa una planta y sus rangos ambientales permitidos.
    /// </summary>
    [Table("plantas")]
    public class Planta
    {
        /// <summary>Identificador de la planta.</summary>
        [Key]
        [Column("id_planta")]
        public int IdPlanta { get; set; }

        /// <summary>Nombre de la especie.</summary>
        [Required(ErrorMessage = "El nombre de la planta es obligatorio")]
        [StringLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Descripción opcional de la planta.</summary>
        [StringLength(255)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }

        /// <summary>Temperatura mínima permitida.</summary>
        [Required]
        [Column("temperatura_minima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMinima { get; set; }

        /// <summary>Temperatura máxima permitida.</summary>
        [Required]
        [Column("temperatura_maxima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMaxima { get; set; }

        /// <summary>Inicio del rango óptimo de temperatura.</summary>
        [Required]
        [Column("temperatura_optima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaOptima { get; set; }

        /// <summary>Humedad mínima permitida.</summary>
        [Required]
        [Column("humedad_minima", TypeName = "decimal(5,2)")]
        public decimal HumedadMinima { get; set; }

        /// <summary>Humedad máxima permitida.</summary>
        [Required]
        [Column("humedad_maxima", TypeName = "decimal(5,2)")]
        public decimal HumedadMaxima { get; set; }

        /// <summary>Inicio del rango óptimo de humedad.</summary>
        [Required]
        [Column("humedad_optima", TypeName = "decimal(5,2)")]
        public decimal HumedadOptima { get; set; }

        /// <summary>Usuario que creó la planta.</summary>
        [Required]
        [Column("id_usuario_creacion")]
        public int IdUsuarioCreacion { get; set; }

        /// <summary>Fecha de la última actualización.</summary>
        [Column("fecha_actualizacion")]
        public DateTime FechaActualizacion { get; set; } = DateTime.Now;

        /// <summary>Indica si la planta está activa.</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        /// <summary>Datos del usuario que creó la planta.</summary>
        [ForeignKey(nameof(IdUsuarioCreacion))]
        public virtual Usuario? UsuarioCreacion { get; set; }

        /// <summary>Invernadero al que está asignada la planta.</summary>
        public virtual Invernadero? Invernadero { get; set; }
    }
}
