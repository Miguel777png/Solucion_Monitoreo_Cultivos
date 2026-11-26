using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Representa un dispositivo ESP32 registrado en el sistema.
    /// </summary>
    [Table("sensores")]
    public class Sensor
    {
        /// <summary>Identificador del sensor.</summary>
        [Key]
        [Column("id_sensor")]
        public int IdSensor { get; set; }

        /// <summary>Invernadero asignado al sensor.</summary>
        [Column("id_invernadero")]
        public int? IdInvernadero { get; set; }

        /// <summary>Código único usado por el ESP32.</summary>
        [Required(ErrorMessage = "El código del dispositivo es obligatorio")]
        [StringLength(50)]
        [Column("codigo_dispositivo")]
        public string CodigoDispositivo { get; set; } = string.Empty;

        /// <summary>Nombre mostrado para el nodo.</summary>
        [Required]
        [StringLength(100)]
        [Column("nombre_nodo")]
        public string NombreNodo { get; set; } = string.Empty;

        /// <summary>Estado de conexión del dispositivo.</summary>
        [Required]
        [StringLength(30)]
        [Column("estado_conexion")]
        public string EstadoConexion { get; set; } = "Desconectado";

        /// <summary>Fecha de la última lectura recibida.</summary>
        [Column("ultima_senal")]
        public DateTime? UltimaSenal { get; set; }

        /// <summary>Indica si el sensor está permitido.</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        /// <summary>Datos del invernadero asignado.</summary>
        [ForeignKey(nameof(IdInvernadero))]
        public virtual Invernadero? Invernadero { get; set; }
    }
}
