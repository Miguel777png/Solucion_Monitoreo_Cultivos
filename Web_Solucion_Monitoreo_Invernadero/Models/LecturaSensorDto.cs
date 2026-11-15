using System.ComponentModel.DataAnnotations;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    public class LecturaSensorDto
    {
        [Required]
        public string CodigoDispositivo { get; set; } = string.Empty;

        [Required]
        public decimal Humedad { get; set; }

        [Required]
        public decimal Temperatura { get; set; }
    }
}