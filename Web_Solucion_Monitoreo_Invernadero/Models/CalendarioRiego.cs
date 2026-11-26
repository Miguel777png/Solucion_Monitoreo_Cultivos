using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa un horario de riego programado.
    /// </summary>
    [Table("calendario_riego")]
    public class CalendarioRiego
    {
        /// <summary>Identificador del horario.</summary>
        [Key]
        [Column("id_calendario_riego")]
        public int IdCalendarioRiego { get; set; }

        /// <summary>Invernadero que se debe regar.</summary>
        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        /// <summary>Usuario que creó el horario.</summary>
        [Required]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        /// <summary>Fecha usada para un riego de una sola vez.</summary>
        [Column("fecha_especifica", TypeName = "date")]
        public DateOnly? FechaEspecifica { get; set; }

        /// <summary>Día usado para un riego semanal.</summary>
        [StringLength(20)]
        [Column("dia_semana")]
        public string? DiaSemana { get; set; }

        /// <summary>Hora programada para el riego.</summary>
        [Required(ErrorMessage = "La hora de riego es obligatoria")]
        [Column("hora_riego", TypeName = "time")]
        public TimeOnly HoraRiego { get; set; }

        /// <summary>Duración del riego en segundos.</summary>
        [Required]
        [Range(1, 3600, ErrorMessage = "El tiempo de riego debe ser al menos 1 segundo")]
        [Column("tiempo_riego_seg")]
        public int TiempoRiegoSeg { get; set; } = 10;

        /// <summary>Fecha hasta la que fue pospuesto.</summary>
        [Column("pospuesto_hasta", TypeName = "date")]
        public DateOnly? PospuestoHasta { get; set; }

        /// <summary>Fecha de creación del horario.</summary>
        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        /// <summary>Indica si el horario está activo.</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        /// <summary>Datos del invernadero relacionado.</summary>
        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }

        /// <summary>Datos del usuario relacionado.</summary>
        [ForeignKey(nameof(IdUsuario))]
        public virtual Usuario? Usuario { get; set; }
    }
}
