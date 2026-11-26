using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa un evento de riego o una alerta ambiental.
    /// </summary>
    [Table("bitacora_riego_alertas")]
    public class BitacoraRiegoAlerta
    {
        /// <summary>Identificador del evento.</summary>
        [Key]
        [Column("id_bitacora")]
        public int IdBitacora { get; set; }

        /// <summary>Invernadero relacionado.</summary>
        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        /// <summary>Tipo de evento registrado.</summary>
        [Required]
        [StringLength(50)]
        [Column("tipo_evento")]
        public string TipoEvento { get; set; } = string.Empty;

        /// <summary>Descripción del evento.</summary>
        [Required]
        [StringLength(255)]
        [Column("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>Tiempo que estuvo activo el actuador.</summary>
        [Column("tiempo_activacion_seg")]
        public int TiempoActivacionSeg { get; set; }

        /// <summary>Humedad registrada en ese momento.</summary>
        [Column("humedad_momento", TypeName = "decimal(5,2)")]
        public decimal HumedadMomento { get; set; }

        /// <summary>Temperatura registrada en ese momento.</summary>
        [Column("temperatura_momento", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMomento { get; set; }

        /// <summary>Fecha y hora del evento.</summary>
        [Required]
        [Column("fecha_hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        /// <summary>Indica si el evento ya fue revisado.</summary>
        [Column("leida")]
        public bool Leida { get; set; } = false;

        /// <summary>Datos del invernadero relacionado.</summary>
        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }
    }
}
