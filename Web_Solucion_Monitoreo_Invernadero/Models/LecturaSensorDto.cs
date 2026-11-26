using System.ComponentModel.DataAnnotations;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Contiene una lectura enviada por un ESP32.
    /// </summary>
    public class LecturaSensorDto
    {
        /// <summary>Código del dispositivo que envía la lectura.</summary>
        [Required]
        public string CodigoDispositivo { get; set; } = string.Empty;

        /// <summary>Humedad medida por el sensor.</summary>
        [Required]
        public decimal Humedad { get; set; }

        /// <summary>Temperatura medida por el sensor.</summary>
        [Required]
        public decimal Temperatura { get; set; }
    }
}
