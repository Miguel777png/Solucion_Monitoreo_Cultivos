using System.ComponentModel.DataAnnotations;

namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    public class ConfirmacionComandoRiegoDto
    {
        [Required]
        [StringLength(50)]
        public string CodigoDispositivo { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int IdComandoRiego { get; set; }

        [Required]
        [StringLength(30)]
        public string Estado { get; set; } = string.Empty;
    }
}
