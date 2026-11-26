namespace Web_Solucion_Monitoreo_Invernadero.Models
{
    /// <summary>
    /// Contiene los datos básicos que se muestran en la página de error.
    /// </summary>
    public class ErrorViewModel
    {
        /// <summary>Identificador de la solicitud que produjo el error.</summary>
        public string? RequestId { get; set; }

        /// <summary>Indica si existe un identificador para mostrar.</summary>
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
