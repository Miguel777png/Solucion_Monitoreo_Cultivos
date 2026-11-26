using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa una lectura ambiental guardada en el historial.
    /// </summary>
    [Table("registro_historico")]
    public class RegistroHistorico
    {
        /// <summary>Identificador del registro.</summary>
        [Key]
        [Column("id_registro")]
        public int IdRegistro { get; set; }

        /// <summary>Invernadero que generó la lectura.</summary>
        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        /// <summary>Humedad registrada.</summary>
        [Required]
        [Column("humedad", TypeName = "decimal(5,2)")]
        public decimal Humedad { get; set; }

        /// <summary>Temperatura registrada.</summary>
        [Required]
        [Column("temperatura", TypeName = "decimal(5,2)")]
        public decimal Temperatura { get; set; }

        /// <summary>Fecha y hora de la lectura.</summary>
        [Required]
        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        /// <summary>Indica si el registro está activo.</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        /// <summary>Datos del invernadero relacionado.</summary>
        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }
    }
}
