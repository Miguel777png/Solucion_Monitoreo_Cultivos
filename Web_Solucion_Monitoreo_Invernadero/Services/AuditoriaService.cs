using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Services
{
    public class AuditoriaService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;

        public AuditoriaService(ApplicationDbContext db, IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        // Devuelve el ID del usuario logueado (por defecto 1 si aún no inició sesión)
        public int ObtenerIdUsuarioActual()
        {
            return _http.HttpContext?.Session.GetInt32("IdUsuario") ?? 1;
        }

        public string ObtenerNombreUsuarioActual()
        {
            return _http.HttpContext?.Session.GetString("NombreUsuario") ?? "Administrador Principal";
        }

        public async Task RegistrarAsync(string accion, string entidadAfectada, string detalle)
        {
            _db.AuditoriaCambios.Add(new AuditoriaCambio
            {
                IdUsuario = ObtenerIdUsuarioActual(),
                Accion = accion,
                EntidadAfectada = entidadAfectada,
                Detalle = detalle,
                FechaHora = DateTime.Now
            });

            await _db.SaveChangesAsync();
        }
    }
}