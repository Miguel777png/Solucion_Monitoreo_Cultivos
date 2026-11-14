using System;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Models;

public partial class CalendarioRiego
{
    public int IdCalendarioRiego { get; set; }

    public int IdPlanta { get; set; }

    public DateOnly? FechaEspecifica { get; set; }

    public string? DiaSemana { get; set; }

    public TimeOnly HoraRiego { get; set; }

    public int TiempoRiegoSeg { get; set; }

    public DateOnly? PospuestoHasta { get; set; }

    public bool? State { get; set; }

    public virtual Planta IdPlantaNavigation { get; set; } = null!;
}
