using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("bitacora_riego_alertas")]
    public class BitacoraRiegoAlerta
    {
        [Key]
        [Column("id_bitacora")]
        public int IdBitacora { get; set; }

        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        [Required]
        [StringLength(50)]
        [Column("tipo_evento")]
        public string TipoEvento { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        [Column("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [Column("tiempo_activacion_seg")]
        public int TiempoActivacionSeg { get; set; }

        [Column("humedad_momento", TypeName = "decimal(5,2)")]
        public decimal HumedadMomento { get; set; }

        [Column("temperatura_momento", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMomento { get; set; }

        [Required]
        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        [Column("leida")]
        public bool Leida { get; set; } = false;

        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }
    }
}