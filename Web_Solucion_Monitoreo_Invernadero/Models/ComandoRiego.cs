using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Contiene los estados permitidos para una orden de riego.
    /// </summary>
    public static class EstadosComandoRiego
    {
        /// <summary>La orden todavía no fue enviada.</summary>
        public const string Pendiente = "Pendiente";
        /// <summary>La orden ya fue entregada al dispositivo.</summary>
        public const string Entregado = "Entregado";
        /// <summary>El dispositivo confirmó que inició la orden.</summary>
        public const string Confirmado = "Confirmado";
        /// <summary>El dispositivo terminó la orden.</summary>
        public const string Completado = "Completado";
        /// <summary>La orden venció antes de completarse.</summary>
        public const string Expirado = "Expirado";
        /// <summary>La orden no se pudo completar.</summary>
        public const string Fallido = "Fallido";
        /// <summary>La orden no fue necesaria.</summary>
        public const string Omitido = "Omitido";
    }

    /// <summary>
    /// Representa una orden de riego que debe recibir un ESP32.
    /// </summary>
    [Table("comandos_riego")]
    public class ComandoRiego
    {
        /// <summary>Identificador de la orden.</summary>
        [Key]
        [Column("id_comando_riego")]
        public int IdComandoRiego { get; set; }

        /// <summary>Invernadero que debe ejecutar la orden.</summary>
        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        /// <summary>Horario que originó la orden.</summary>
        [Column("id_calendario_riego")]
        public int? IdCalendarioRiego { get; set; }

        /// <summary>Estado actual de la orden.</summary>
        [Required]
        [StringLength(30)]
        [Column("estado")]
        public string Estado { get; set; } = EstadosComandoRiego.Pendiente;

        /// <summary>Duración del riego en segundos.</summary>
        [Required]
        [Range(1, 3600)]
        [Column("duracion_seg")]
        public int DuracionSeg { get; set; }

        /// <summary>Prioridad usada para ordenar las solicitudes.</summary>
        [Required]
        [Column("prioridad")]
        public int Prioridad { get; set; } = 50;

        /// <summary>Clave que evita crear la misma orden más de una vez.</summary>
        [Required]
        [StringLength(100)]
        [Column("clave_idempotencia")]
        public string ClaveIdempotencia { get; set; } = string.Empty;

        /// <summary>Fecha en que se creó la orden.</summary>
        [Required]
        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        /// <summary>Fecha límite para entregar la orden.</summary>
        [Required]
        [Column("fecha_expiracion")]
        public DateTime FechaExpiracion { get; set; }

        /// <summary>Fecha en que fue entregada al ESP32.</summary>
        [Column("fecha_entrega")]
        public DateTime? FechaEntrega { get; set; }

        /// <summary>Fecha en que el ESP32 confirmó la orden.</summary>
        [Column("fecha_confirmacion")]
        public DateTime? FechaConfirmacion { get; set; }

        /// <summary>Fecha en que terminó la orden.</summary>
        [Column("fecha_finalizacion")]
        public DateTime? FechaFinalizacion { get; set; }

        /// <summary>Detalle breve del resultado.</summary>
        [StringLength(255)]
        [Column("detalle")]
        public string? Detalle { get; set; }

        /// <summary>Datos del invernadero relacionado.</summary>
        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }

        /// <summary>Datos del horario relacionado.</summary>
        [ForeignKey(nameof(IdCalendarioRiego))]
        public virtual CalendarioRiego? CalendarioRiego { get; set; }
    }
}
