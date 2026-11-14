using System;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Models;

public partial class Planta
{
    public int IdPlanta { get; set; }

    public string Nombre { get; set; } = null!;

    public decimal TemperaturaMaxima { get; set; }

    public decimal TemperaturaMinima { get; set; }

    public decimal HumedadMaxima { get; set; }

    public decimal HumedadMinima { get; set; }

    public bool? State { get; set; }

    public virtual ICollection<CalendarioRiego> CalendarioRiegos { get; set; } = new List<CalendarioRiego>();

    public virtual Invernadero? Invernadero { get; set; }
}
