using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa un invernadero junto con su planta y configuración.
    /// </summary>
    [Table("invernaderos")]
    public class Invernadero
    {
        /// <summary>Identificador del invernadero.</summary>
        [Key]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        /// <summary>Nombre del invernadero.</summary>
        [Required(ErrorMessage = "El nombre del invernadero es obligatorio")]
        [StringLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        /// <summary>Ubicación o sector del invernadero.</summary>
        [Required(ErrorMessage = "La ubicación es obligatoria")]
        [StringLength(150)]
        [Column("ubicacion")]
        public string Ubicacion { get; set; } = string.Empty;

        /// <summary>Planta asignada al invernadero.</summary>
        [Required]
        [Column("id_planta")]
        public int IdPlanta { get; set; }

        /// <summary>Frecuencia de guardado del historial en minutos.</summary>
        [Required]
        [Range(1, 1440, ErrorMessage = "La frecuencia debe estar entre 1 y 1440 minutos")]
        [Column("frecuencia_registro_min")]
        public int FrecuenciaRegistroMin { get; set; } = 60;

        /// <summary>Usuario que realizó la última modificación.</summary>
        [Required]
        [Column("id_usuario_modificacion")]
        public int IdUsuarioModificacion { get; set; }

        /// <summary>Fecha de la última modificación.</summary>
        [Column("fecha_modificacion")]
        public DateTime FechaModificacion { get; set; } = DateTime.Now;

        /// <summary>Indica si el invernadero está activo.</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        /// <summary>Datos de la planta asignada.</summary>
        [ForeignKey(nameof(IdPlanta))]
        public virtual Planta? Planta { get; set; }

        /// <summary>Datos del usuario que modificó el invernadero.</summary>
        [ForeignKey(nameof(IdUsuarioModificacion))]
        public virtual Usuario? UsuarioModificacion { get; set; }

        /// <summary>Sensores relacionados con el invernadero.</summary>
        public virtual ICollection<Sensor> Sensores { get; set; } = new List<Sensor>();
        /// <summary>Horarios de riego del invernadero.</summary>
        public virtual ICollection<CalendarioRiego> CalendariosRiego { get; set; } = new List<CalendarioRiego>();
        /// <summary>Lecturas históricas del invernadero.</summary>
        public virtual ICollection<RegistroHistorico> RegistrosHistoricos { get; set; } = new List<RegistroHistorico>();
        /// <summary>Eventos y alertas del invernadero.</summary>
        public virtual ICollection<BitacoraRiegoAlerta> BitacoraEventos { get; set; } = new List<BitacoraRiegoAlerta>();
    }
}
