using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("registro_historico")]
    public class RegistroHistorico
    {
        [Key]
        [Column("id_registro")]
        public int IdRegistro { get; set; }

        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        [Required]
        [Column("humedad", TypeName = "decimal(5,2)")]
        public decimal Humedad { get; set; }

        [Required]
        [Column("temperatura", TypeName = "decimal(5,2)")]
        public decimal Temperatura { get; set; }

        [Required]
        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        [Column("state")]
        public bool State { get; set; } = true;

        // Llave Foránea
        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }
    }
}