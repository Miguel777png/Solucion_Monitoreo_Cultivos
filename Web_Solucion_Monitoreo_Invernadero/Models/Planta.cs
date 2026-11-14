using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("plantas")]
    public class Planta
    {
        [Key]
        [Column("id_planta")]
        public int IdPlanta { get; set; }

        [Required(ErrorMessage = "El nombre de la planta es obligatorio")]
        [StringLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [Column("temperatura_maxima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMaxima { get; set; }

        [Required]
        [Column("temperatura_minima", TypeName = "decimal(5,2)")]
        public decimal TemperaturaMinima { get; set; }

        [Required]
        [Column("humedad_maxima", TypeName = "decimal(5,2)")]
        public decimal HumedadMaxima { get; set; }

        [Required]
        [Column("humedad_minima", TypeName = "decimal(5,2)")]
        public decimal HumedadMinima { get; set; }

        [Column("state")]
        public bool State { get; set; } = true;

        // Relación 1:1 con Invernadero
        public virtual Invernadero? Invernadero { get; set; }

        // Relación 1:N con CalendarioRiego
        public virtual ICollection<CalendarioRiego> CalendariosRiego { get; set; } = new List<CalendarioRiego>();
    }
}