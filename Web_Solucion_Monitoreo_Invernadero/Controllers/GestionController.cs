using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    public class GestionController : Controller
    {
        private readonly ApplicationDbContext _db;

        public GestionController(ApplicationDbContext db)
        {
            _db = db;
        }

        // 1. CONFIGURACIÓN DE INVERNADEROS Y UMBRALES DE PLANTAS
        public async Task<IActionResult> Configuracion()
        {
            var invernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Include(i => i.Sensores)
                .Where(i => i.State)
                .ToListAsync();

            return View(invernaderos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarParametros(
            int idInvernadero,
            int frecuenciaRegistroMin,
            decimal temperaturaMinima,
            decimal temperaturaMaxima,
            decimal humedadMinima,
            decimal humedadMaxima)
        {
            var inv = await _db.Invernaderos
                .Include(i => i.Planta)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero);

            if (inv != null && inv.Planta != null)
            {
                inv.FrecuenciaRegistroMin = frecuenciaRegistroMin;
                inv.Planta.TemperaturaMinima = temperaturaMinima;
                inv.Planta.TemperaturaMaxima = temperaturaMaxima;
                inv.Planta.HumedadMinima = humedadMinima;
                inv.Planta.HumedadMaxima = humedadMaxima;

                await _db.SaveChangesAsync();
                TempData["MensajeExito"] = $"Parámetros actualizados para {inv.Planta.Nombre}. Se aplicarán en la próxima lectura del ESP32.";
            }

            return RedirectToAction(nameof(Configuracion));
        }

        // 2. CALENDARIO DE RIEGO INTELIGENTE
        public async Task<IActionResult> Calendario()
        {
            ViewBag.Plantas = await _db.Plantas
                .Include(p => p.Invernadero)
                .Where(p => p.State)
                .ToListAsync();

            var horarios = await _db.CalendarioRiego
                .Include(c => c.Planta)
                .Where(c => c.State)
                .OrderBy(c => c.HoraRiego)
                .ToListAsync();

            return View(horarios);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearHorario(
            int idPlanta,
            string? diaSemana,
            DateOnly? fechaEspecifica,
            TimeOnly horaRiego,
            int tiempoRiegoSeg)
        {
            var nuevo = new CalendarioRiego
            {
                IdPlanta = idPlanta,
                DiaSemana = fechaEspecifica.HasValue ? null : diaSemana,
                FechaEspecifica = fechaEspecifica,
                HoraRiego = horaRiego,
                TiempoRiegoSeg = tiempoRiegoSeg > 0 ? tiempoRiegoSeg : 10,
                State = true
            };

            _db.CalendarioRiego.Add(nuevo);
            await _db.SaveChangesAsync();

            TempData["MensajeExito"] = "Horario de riego programado correctamente.";
            return RedirectToAction(nameof(Calendario));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarHorario(int idCalendario)
        {
            var item = await _db.CalendarioRiego.FindAsync(idCalendario);
            if (item != null)
            {
                item.State = false; // Borrado lógico respetando el campo 'state'
                await _db.SaveChangesAsync();
                TempData["MensajeExito"] = "Horario desactivado correctamente.";
            }
            return RedirectToAction(nameof(Calendario));
        }

        // 3. REGISTRO HISTÓRICO (MYSQL)
        public async Task<IActionResult> Historial(int idInvernadero = 1)
        {
            ViewBag.Invernaderos = await _db.Invernaderos.Where(i => i.State).ToListAsync();
            ViewBag.InvernaderoSeleccionado = idInvernadero;

            var registros = await _db.RegistroHistorico
                .Where(r => r.IdInvernadero == idInvernadero && r.State)
                .OrderByDescending(r => r.FechaHora)
                .Take(100)
                .ToListAsync();

            return View(registros);
        }
    }
}