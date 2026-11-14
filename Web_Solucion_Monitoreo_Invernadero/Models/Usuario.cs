using System;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Models;

public partial class Usuario
{
    public int IdUsuario { get; set; }

    public string Nombre { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public bool? State { get; set; }

    public virtual ICollection<Invernadero> Invernaderos { get; set; } = new List<Invernadero>();
}
