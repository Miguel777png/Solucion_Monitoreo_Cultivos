using System.ComponentModel.DataAnnotations;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Contiene la confirmación de una orden enviada por el ESP32.
    /// </summary>
    public class ConfirmacionComandoRiegoDto
    {
        /// <summary>Código del dispositivo que confirma.</summary>
        [Required]
        [StringLength(50)]
        public string CodigoDispositivo { get; set; } = string.Empty;

        /// <summary>Identificador de la orden confirmada.</summary>
        [Range(1, int.MaxValue)]
        public int IdComandoRiego { get; set; }

        /// <summary>Nuevo estado informado por el dispositivo.</summary>
        [Required]
        [StringLength(30)]
        public string Estado { get; set; } = string.Empty;
    }
}
