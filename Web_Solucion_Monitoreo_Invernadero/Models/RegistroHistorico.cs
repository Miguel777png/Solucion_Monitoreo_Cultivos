using System;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Models;

public partial class RegistroHistorico
{
    public int IdRegistro { get; set; }

    public int IdInvernadero { get; set; }

    public decimal Humedad { get; set; }

    public decimal Temperatura { get; set; }

    public DateTime FechaHora { get; set; }

    public bool? State { get; set; }

    public virtual Invernadero IdInvernaderoNavigation { get; set; } = null!;
}
