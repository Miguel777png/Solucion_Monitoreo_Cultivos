using System;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Models;

public partial class BitacoraRiegoAlerta
{
    public int IdEvento { get; set; }

    public int IdInvernadero { get; set; }

    public string TipoEvento { get; set; } = null!;

    public string Descripcion { get; set; } = null!;

    public int? TiempoActivacionSeg { get; set; }

    public decimal? HumedadMomento { get; set; }

    public decimal? TemperaturaMomento { get; set; }

    public DateTime FechaHora { get; set; }

    public bool Leida { get; set; }

    public virtual Invernadero IdInvernaderoNavigation { get; set; } = null!;
}
