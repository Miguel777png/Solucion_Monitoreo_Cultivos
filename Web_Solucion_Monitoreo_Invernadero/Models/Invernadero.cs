using System;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Models;

public partial class Invernadero
{
    public int IdInvernadero { get; set; }

    public int IdUsuario { get; set; }

    public int IdPlanta { get; set; }

    public string Ubicacion { get; set; } = null!;

    public int FrecuenciaRegistroMin { get; set; }

    public bool? State { get; set; }

    public virtual ICollection<BitacoraRiegoAlerta> BitacoraRiegoAlerta { get; set; } = new List<BitacoraRiegoAlerta>();

    public virtual Planta IdPlantaNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<RegistroHistorico> RegistroHistoricos { get; set; } = new List<RegistroHistorico>();

    public virtual ICollection<Sensore> Sensores { get; set; } = new List<Sensore>();
}
