using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("calendario_riego")]
    public class CalendarioRiego
    {
        [Key]
        [Column("id_calendario_riego")]
        public int IdCalendarioRiego { get; set; }

        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        [Required]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("fecha_especifica", TypeName = "date")]
        public DateOnly? FechaEspecifica { get; set; }

        [StringLength(20)]
        [Column("dia_semana")]
        public string? DiaSemana { get; set; }

        [Required(ErrorMessage = "La hora de riego es obligatoria")]
        [Column("hora_riego", TypeName = "time")]
        public TimeOnly HoraRiego { get; set; }

        [Required]
        [Range(1, 3600, ErrorMessage = "El tiempo de riego debe ser al menos 1 segundo")]
        [Column("tiempo_riego_seg")]
        public int TiempoRiegoSeg { get; set; } = 10;

        [Column("pospuesto_hasta", TypeName = "date")]
        public DateOnly? PospuestoHasta { get; set; }

        [Column("fecha_creacion")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        [Column("state")]
        public bool State { get; set; } = true;

        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }

        [ForeignKey(nameof(IdUsuario))]
        public virtual Usuario? Usuario { get; set; }
    }
}