using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [VerificarAcceso]
    public class PlantasController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        public PlantasController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        // LISTADO DEL CATÁLOGO CON BÚSQUEDA Y FILTRO DE DISPONIBILIDAD
        public async Task<IActionResult> Index(string? busqueda, string? estadoFiltro)
        {
            ViewBag.Busqueda = busqueda;
            ViewBag.EstadoFiltro = estadoFiltro;

            var consulta = _db.Plantas
                .Include(p => p.UsuarioCreacion)
                .Include(p => p.Invernadero)
                .Where(p => p.State);

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                string termino = busqueda.Trim();
                consulta = consulta.Where(p =>
                    p.Nombre.Contains(termino) ||
                    (p.Descripcion != null && p.Descripcion.Contains(termino)));
            }

            if (estadoFiltro == "disponible")
            {
                consulta = consulta.Where(p => p.Invernadero == null || !p.Invernadero.State);
            }
            else if (estadoFiltro == "asignada")
            {
                consulta = consulta.Where(p => p.Invernadero != null && p.Invernadero.State);
            }

            var plantas = await consulta
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
            string temperaturaMinima,
            string temperaturaMaxima,
            string humedadMinima,
            string humedadMaxima)
        {
            if (!TryParsearUmbrales(temperaturaMinima, temperaturaMaxima, humedadMinima, humedadMaxima,
                out decimal tMin, out decimal tMax, out decimal hMin, out decimal hMax, out string error))
            {
                TempData["MensajeError"] = error;
                return RedirectToAction(nameof(Index));
            }

            int idUsuario = _auditoria.ObtenerIdUsuarioActual();

            var nueva = new Planta
            {
                Nombre = nombre.Trim(),
                Descripcion = descripcion?.Trim(),
                TemperaturaMinima = tMin,
                TemperaturaMaxima = tMax,
                HumedadMinima = hMin,
                HumedadMaxima = hMax,
                IdUsuarioCreacion = idUsuario,
                FechaActualizacion = DateTime.Now,
                State = true
            };

            _db.Plantas.Add(nueva);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "CREAR_PLANTA",
                "Plantas",
                $"Ingresó la planta '{nueva.Nombre}' (Temp: {tMin}-{tMax}°C, Hum: {hMin}-{hMax}%)."
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
            string temperaturaMinima,
            string temperaturaMaxima,
            string humedadMinima,
            string humedadMaxima)
        {
            var planta = await _db.Plantas.FindAsync(idPlanta);
            if (planta == null) return RedirectToAction(nameof(Index));

            if (!TryParsearUmbrales(temperaturaMinima, temperaturaMaxima, humedadMinima, humedadMaxima,
                out decimal tMin, out decimal tMax, out decimal hMin, out decimal hMax, out string error))
            {
                TempData["MensajeError"] = error;
                return RedirectToAction(nameof(Index));
            }

            planta.Nombre = nombre.Trim();
            planta.Descripcion = descripcion?.Trim();
            planta.TemperaturaMinima = tMin;
            planta.TemperaturaMaxima = tMax;
            planta.HumedadMinima = hMin;
            planta.HumedadMaxima = hMax;
            planta.FechaActualizacion = DateTime.Now;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "EDITAR_PLANTA",
                "Plantas",
                $"Actualizó umbrales de '{planta.Nombre}' (Temp: {tMin}-{tMax}°C, Hum: {hMin}-{hMax}%)."
            );

            TempData["MensajeExito"] = $"Umbrales de '{planta.Nombre}' actualizados correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ELIMINAR LÓGICAMENTE UNA PLANTA (SOLO SI NO ESTÁ ASIGNADA)
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

        // NORMALIZADOR DECIMAL SEGURO (SOPORTA COMA Y PUNTO SIN MULTIPLICAR POR 10/100)
        private static bool TryParsearUmbrales(
            string sTMin, string sTMax, string sHMin, string sHMax,
            out decimal tMin, out decimal tMax, out decimal hMin, out decimal hMax,
            out string error)
        {
            tMin = tMax = hMin = hMax = 0;
            error = string.Empty;

            bool ok1 = decimal.TryParse(sTMin?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out tMin);
            bool ok2 = decimal.TryParse(sTMax?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out tMax);
            bool ok3 = decimal.TryParse(sHMin?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out hMin);
            bool ok4 = decimal.TryParse(sHMax?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out hMax);

            if (!ok1 || !ok2 || !ok3 || !ok4)
            {
                error = "Formato numérico inválido en los umbrales.";
                return false;
            }

            if (tMin < -10 || tMax > 60 || tMin >= tMax)
            {
                error = "La temperatura debe estar entre -10°C y 60°C, y la mínima debe ser menor que la máxima.";
                return false;
            }

            if (hMin < 0 || hMax > 100 || hMin >= hMax)
            {
                error = "La humedad debe estar entre 0% y 100%, y la mínima debe ser menor que la máxima.";
                return false;
            }

            tMin = Math.Round(tMin, 2);
            tMax = Math.Round(tMax, 2);
            hMin = Math.Round(hMin, 2);
            hMax = Math.Round(hMax, 2);
            return true;
        }
    }
}