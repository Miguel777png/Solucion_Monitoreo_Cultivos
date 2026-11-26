using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    /// <summary>
    /// Administra los horarios de riego de cada invernadero.
    /// </summary>
    [VerificarAcceso]
    public class CalendarioController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        /// <summary>
        /// Inicializa el controlador de calendarios.
        /// </summary>
        /// <param name="db">Contexto de la base de datos.</param>
        /// <param name="auditoria">Servicio que registra los cambios.</param>
        public CalendarioController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        /// <summary>
        /// Lista los horarios activos y permite filtrarlos por invernadero.
        /// </summary>
        /// <param name="idInvernaderoFiltro">Invernadero seleccionado para el filtro.</param>
        /// <returns>La vista con los horarios encontrados.</returns>
        public async Task<IActionResult> Index(int? idInvernaderoFiltro)
        {
            ViewBag.Invernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            ViewBag.FiltroSeleccionado = idInvernaderoFiltro;

            var consulta = _db.CalendarioRiego
                .Include(c => c.Invernadero)
                    .ThenInclude(i => i!.Planta)
                .Include(c => c.Usuario)
                .Where(c => c.State);

            if (idInvernaderoFiltro.HasValue && idInvernaderoFiltro.Value > 0)
            {
                consulta = consulta.Where(c => c.IdInvernadero == idInvernaderoFiltro.Value);
            }

            var horarios = await consulta
                .OrderBy(c => c.IdInvernadero)
                .ThenBy(c => c.HoraRiego)
                .ToListAsync();

            return View(horarios);
        }

        /// <summary>
        /// Crea un nuevo horario de riego.
        /// </summary>
        /// <param name="idInvernadero">Invernadero que recibirá el horario.</param>
        /// <param name="diaSemana">Día para un riego semanal.</param>
        /// <param name="fechaEspecifica">Fecha para un riego de una sola vez.</param>
        /// <param name="horaRiego">Hora en que se debe regar.</param>
        /// <param name="tiempoRiegoSeg">Duración del riego en segundos.</param>
        /// <returns>Una redirección al listado de calendarios.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            int idInvernadero,
            string? diaSemana,
            DateOnly? fechaEspecifica,
            TimeOnly horaRiego,
            int tiempoRiegoSeg)
        {
            var inv = await _db.Invernaderos
                .Include(i => i.Planta)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero && i.State);

            if (inv == null)
            {
                TempData["MensajeError"] = "Invernadero no válido.";
                return RedirectToAction(nameof(Index));
            }

            int idUsuario = _auditoria.ObtenerIdUsuarioActual();
            int duracion = tiempoRiegoSeg > 0 ? tiempoRiegoSeg : 10;

            var nuevo = new CalendarioRiego
            {
                IdInvernadero = idInvernadero,
                IdUsuario = idUsuario,
                FechaEspecifica = fechaEspecifica,
                DiaSemana = fechaEspecifica.HasValue ? null : diaSemana,
                HoraRiego = horaRiego,
                TiempoRiegoSeg = duracion,
                FechaCreacion = DateTime.Now,
                State = true
            };

            _db.CalendarioRiego.Add(nuevo);
            await _db.SaveChangesAsync();

            string cuando = fechaEspecifica.HasValue
                ? $"fecha {fechaEspecifica.Value:dd/MM/yyyy}"
                : $"todos los {diaSemana}";

            await _auditoria.RegistrarAsync(
                "CREAR_RIEGO_PROGRAMADO",
                inv.Nombre,
                $"Estableció riego ({cuando} a las {horaRiego:HH:mm}, {duracion}s) para '{inv.Planta?.Nombre}'."
            );

            TempData["MensajeExito"] = $"Riego programado para {inv.Nombre} por Usuario ID #{idUsuario}.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Desactiva un horario de riego existente.
        /// </summary>
        /// <param name="idCalendarioRiego">Identificador del horario.</param>
        /// <returns>Una redirección al listado de calendarios.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int idCalendarioRiego)
        {
            var item = await _db.CalendarioRiego
                .Include(c => c.Invernadero)
                .FirstOrDefaultAsync(c => c.IdCalendarioRiego == idCalendarioRiego);

            if (item != null)
            {
                item.State = false;
                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    "DESACTIVAR_RIEGO_PROGRAMADO",
                    item.Invernadero?.Nombre ?? $"Invernadero #{item.IdInvernadero}",
                    $"Canceló horario programado de las {item.HoraRiego:HH:mm} (ID Horario #{item.IdCalendarioRiego})."
                );

                TempData["MensajeExito"] = "Horario de riego desactivado.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
