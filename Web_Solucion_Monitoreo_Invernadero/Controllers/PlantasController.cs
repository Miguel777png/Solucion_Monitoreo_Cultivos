using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    public class PlantasController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        public PlantasController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        // LISTADO DEL CATÁLOGO DE PLANTAS
        public async Task<IActionResult> Index()
        {
            var plantas = await _db.Plantas
                .Include(p => p.UsuarioCreacion)
                .Include(p => p.Invernadero)
                .Where(p => p.State)
                .OrderBy(p => p.IdPlanta)
                .ToListAsync();

            return View(plantas);
        }

        // CREAR NUEVA PLANTA EN EL CATÁLOGO
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            string nombre,
            string? descripcion,
            decimal temperaturaMinima,
            decimal temperaturaMaxima,
            decimal humedadMinima,
            decimal humedadMaxima)
        {
            if (temperaturaMinima >= temperaturaMaxima || humedadMinima >= humedadMaxima)
            {
                TempData["MensajeError"] = "Los valores mínimos deben ser estrictamente menores que los máximos.";
                return RedirectToAction(nameof(Index));
            }

            int idUsuario = _auditoria.ObtenerIdUsuarioActual();

            var nueva = new Planta
            {
                Nombre = nombre.Trim(),
                Descripcion = descripcion?.Trim(),
                TemperaturaMinima = temperaturaMinima,
                TemperaturaMaxima = temperaturaMaxima,
                HumedadMinima = humedadMinima,
                HumedadMaxima = humedadMaxima,
                IdUsuarioCreacion = idUsuario,
                FechaActualizacion = DateTime.Now,
                State = true
            };

            _db.Plantas.Add(nueva);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "CREAR_PLANTA",
                "Plantas",
                $"Ingresó la planta '{nueva.Nombre}' (Temp: {temperaturaMinima}-{temperaturaMaxima}°C, Hum: {humedadMinima}-{humedadMaxima}%)."
            );

            TempData["MensajeExito"] = $"Planta '{nueva.Nombre}' agregada al catálogo por Usuario ID #{idUsuario}.";
            return RedirectToAction(nameof(Index));
        }

        // EDITAR UMBRALES DE UNA PLANTA EXISTENTE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int idPlanta,
            string nombre,
            string? descripcion,
            decimal temperaturaMinima,
            decimal temperaturaMaxima,
            decimal humedadMinima,
            decimal humedadMaxima)
        {
            var planta = await _db.Plantas.FindAsync(idPlanta);
            if (planta == null) return RedirectToAction(nameof(Index));

            if (temperaturaMinima >= temperaturaMaxima || humedadMinima >= humedadMaxima)
            {
                TempData["MensajeError"] = "Los valores mínimos deben ser estrictamente menores que los máximos.";
                return RedirectToAction(nameof(Index));
            }

            planta.Nombre = nombre.Trim();
            planta.Descripcion = descripcion?.Trim();
            planta.TemperaturaMinima = temperaturaMinima;
            planta.TemperaturaMaxima = temperaturaMaxima;
            planta.HumedadMinima = humedadMinima;
            planta.HumedadMaxima = humedadMaxima;
            planta.FechaActualizacion = DateTime.Now;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "EDITAR_PLANTA",
                "Plantas",
                $"Actualizó umbrales de '{planta.Nombre}' (Temp: {temperaturaMinima}-{temperaturaMaxima}°C, Hum: {humedadMinima}-{humedadMaxima}%)."
            );

            TempData["MensajeExito"] = $"Umbrales de '{planta.Nombre}' actualizados.";
            return RedirectToAction(nameof(Index));
        }

        // ELIMINAR LÓGICAMENTE UNA PLANTA (SOLO SI NO ESTÁ ASIGNADA A UN INVERNADERO)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int idPlanta)
        {
            var planta = await _db.Plantas
                .Include(p => p.Invernadero)
                .FirstOrDefaultAsync(p => p.IdPlanta == idPlanta);

            if (planta == null) return RedirectToAction(nameof(Index));

            if (planta.Invernadero != null && planta.Invernadero.State)
            {
                TempData["MensajeError"] = $"No puedes eliminar '{planta.Nombre}' porque está asignada a '{planta.Invernadero.Nombre}'.";
                return RedirectToAction(nameof(Index));
            }

            planta.State = false;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "ELIMINAR_PLANTA",
                "Plantas",
                $"Desactivó del catálogo la planta '{planta.Nombre}' (ID #{planta.IdPlanta})."
            );

            TempData["MensajeExito"] = $"Planta '{planta.Nombre}' eliminada del catálogo.";
            return RedirectToAction(nameof(Index));
        }
    }
}