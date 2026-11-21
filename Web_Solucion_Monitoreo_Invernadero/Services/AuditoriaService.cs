using Web_Solucion_Monitoreo_Invernadero.Database;
using System.Security.Claims;
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
            var httpContext = _http.HttpContext;
            var idClaim = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (int.TryParse(idClaim, out int idUsuario))
            {
                return idUsuario;
            }

            return httpContext?.Session.GetInt32("IdUsuario")
                ?? throw new InvalidOperationException("No existe un usuario autenticado para registrar la auditoría.");
        }

        public string ObtenerNombreUsuarioActual()
        {
            var httpContext = _http.HttpContext;
            return httpContext?.User.Identity?.Name
                ?? httpContext?.Session.GetString("NombreUsuario")
                ?? "Usuario autenticado";
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
