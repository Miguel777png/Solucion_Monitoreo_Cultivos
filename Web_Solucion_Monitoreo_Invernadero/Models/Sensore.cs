using System;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Models;

public partial class Sensore
{
    public int IdSensor { get; set; }

    public int IdInvernadero { get; set; }

    public string CodigoDispositivo { get; set; } = null!;

    public string TipoConexion { get; set; } = null!;

    public string EstadoConexion { get; set; } = null!;

    public DateTime? UltimaSenal { get; set; }

    public bool? State { get; set; }

    public virtual Invernadero IdInvernaderoNavigation { get; set; } = null!;
}
