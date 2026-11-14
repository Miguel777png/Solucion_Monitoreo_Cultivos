using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("invernaderos")]
    public class Invernadero
    {
        [Key]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        [Required]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Required]
        [Column("id_planta")]
        public int IdPlanta { get; set; }

        [Required(ErrorMessage = "La ubicación es obligatoria")]
        [StringLength(150)]
        [Column("ubicacion")]
        public string Ubicacion { get; set; } = string.Empty;

        [Required]
        [Range(1, 1440, ErrorMessage = "La frecuencia debe estar entre 1 y 1440 minutos")]
        [Column("frecuencia_registro_min")]
        public int FrecuenciaRegistroMin { get; set; } = 60;

        [Column("state")]
        public bool State { get; set; } = true;

        // Llaves Foráneas
        [ForeignKey(nameof(IdUsuario))]
        public virtual Usuario? Usuario { get; set; }

        [ForeignKey(nameof(IdPlanta))]
        public virtual Planta? Planta { get; set; }

        // Relaciones 1:N hijas
        public virtual ICollection<Sensor> Sensores { get; set; } = new List<Sensor>();
        public virtual ICollection<RegistroHistorico> RegistrosHistoricos { get; set; } = new List<RegistroHistorico>();
        public virtual ICollection<BitacoraRiegoAlerta> BitacoraEventos { get; set; } = new List<BitacoraRiegoAlerta>();
    }
}