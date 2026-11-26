using Web_Solucion_Monitoreo_Invernadero.Database;
using System.Security.Claims;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Services
{


    /// <summary>
    /// Registra las acciones realizadas por los usuarios del sistema.
    /// </summary>
    public class AuditoriaService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;

        /// <summary>
        /// Inicializa el servicio de auditoría.
        /// </summary>
        /// <param name="db">Contexto de la base de datos.</param>
        /// <param name="http">Acceso al contexto HTTP actual.</param>
        public AuditoriaService(ApplicationDbContext db, IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        /// <summary>
        /// Obtiene el identificador del usuario que inició sesión.
        /// </summary>
        /// <returns>El identificador encontrado en los claims o la sesión.</returns>
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

        /// <summary>
        /// Obtiene el nombre del usuario que inició sesión.
        /// </summary>
        /// <returns>El nombre encontrado en la autenticación o la sesión.</returns>
        public string ObtenerNombreUsuarioActual()
        {
            var httpContext = _http.HttpContext;
            return httpContext?.User.Identity?.Name
                ?? httpContext?.Session.GetString("NombreUsuario")
                ?? "Usuario autenticado";
        }

        /// <summary>
        /// Guarda una acción en la tabla de auditoría.
        /// </summary>
        /// <param name="accion">Nombre de la acción realizada.</param>
        /// <param name="entidadAfectada">Elemento que fue modificado.</param>
        /// <param name="detalle">Descripción breve del cambio.</param>
        /// <returns>Una tarea que representa el guardado.</returns>
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
