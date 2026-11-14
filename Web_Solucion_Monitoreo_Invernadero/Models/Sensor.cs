using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    [Table("sensores")]
    public class Sensor
    {
        [Key]
        [Column("id_sensor")]
        public int IdSensor { get; set; }

        [Required]
        [Column("id_invernadero")]
        public int IdInvernadero { get; set; }

        [Required(ErrorMessage = "El código del dispositivo es obligatorio")]
        [StringLength(50)]
        [Column("codigo_dispositivo")]
        public string CodigoDispositivo { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        [Column("tipo_conexion")]
        public string TipoConexion { get; set; } = "WiFi";

        [Required]
        [StringLength(20)]
        [Column("estado_conexion")]
        public string EstadoConexion { get; set; } = "Desconectado";

        [Column("ultima_senal")]
        public DateTime? UltimaSenal { get; set; }

        [Column("state")]
        public bool State { get; set; } = true;

        // Llave Foránea
        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }
    }
}