using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    public static class EstadosComandoRiego
    {
        public const string Pendiente = "Pendiente";
        public const string Entregado = "Entregado";
        public const string Confirmado = "Confirmado";
        public const string Completado = "Completado";
        public const string Expirado = "Expirado";
        public const string Fallido = "Fallido";
    }

    [Table("comandos_riego")]
    public class ComandoRiego
    {
        [Key]
        [Column("id_comando_riego")]
        public int IdComandoRiego { get; set; }

        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        [Column("id_calendario_riego")]
        public int? IdCalendarioRiego { get; set; }

        [Required]
        [StringLength(30)]
        [Column("estado")]
        public string Estado { get; set; } = EstadosComandoRiego.Pendiente;

        [Required]
        [Range(1, 3600)]
        [Column("duracion_seg")]
        public int DuracionSeg { get; set; }

        [Required]
        [Column("prioridad")]
        public int Prioridad { get; set; } = 50;

        [Required]
        [StringLength(100)]
        [Column("clave_idempotencia")]
        public string ClaveIdempotencia { get; set; } = string.Empty;

        [Required]
        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; }

        [Required]
        [Column("fecha_expiracion")]
        public DateTime FechaExpiracion { get; set; }

        [Column("fecha_entrega")]
        public DateTime? FechaEntrega { get; set; }

        [Column("fecha_confirmacion")]
        public DateTime? FechaConfirmacion { get; set; }

        [Column("fecha_finalizacion")]
        public DateTime? FechaFinalizacion { get; set; }

        [StringLength(255)]
        [Column("detalle")]
        public string? Detalle { get; set; }

        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }

        [ForeignKey(nameof(IdCalendarioRiego))]
        public virtual CalendarioRiego? CalendarioRiego { get; set; }
    }
}
