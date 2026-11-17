using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    public class CalendarioController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        public CalendarioController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

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