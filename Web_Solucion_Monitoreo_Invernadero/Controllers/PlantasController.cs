using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{


    /// Controlador CRUD para el catálogo botánico de especies de orquídeas.
    /// Gestiona el alta, edición y borrado lógico de plantas con búsqueda y filtros de disponibilidad,
    /// validando y normalizando los umbrales mínimos, óptimos y máximos de temperatura y humedad,
    /// e impidiendo eliminar especies que estén asignadas a un invernadero activo.

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
            string? temperaturaOptima,
            string temperaturaMaxima,
            string humedadMinima,
            string? humedadOptima,
            string humedadMaxima)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["MensajeError"] = "El nombre de la especie es obligatorio.";
                return RedirectToAction(nameof(Index));
            }

            if (!TryParsearUmbrales(
                temperaturaMinima, temperaturaOptima, temperaturaMaxima,
                humedadMinima, humedadOptima, humedadMaxima,
                out decimal tMin, out decimal tOpt, out decimal tMax,
                out decimal hMin, out decimal hOpt, out decimal hMax,
                out string error))
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
                TemperaturaOptima = tOpt,
                TemperaturaMaxima = tMax,
                HumedadMinima = hMin,
                HumedadOptima = hOpt,
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
                $"Ingresó la planta '{nueva.Nombre}' (Temp: {tMin}/{tOpt}/{tMax}°C, Hum: {hMin}/{hOpt}/{hMax}%)."
            );

            TempData["MensajeExito"] = $"Planta '{nueva.Nombre}' agregada al catálogo por Usuario ID #{idUsuario}.";
            return RedirectToAction(nameof(Index));
        }


        // EDITAR UMBRALES DE UNA PLANTA EXISTENTE (CON TRAZABILIDAD ANTES -> DESPUÉS)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int idPlanta,
            string nombre,
            string? descripcion,
            string temperaturaMinima,
            string? temperaturaOptima,
            string temperaturaMaxima,
            string humedadMinima,
            string? humedadOptima,
            string humedadMaxima)
        {
            var planta = await _db.Plantas.FindAsync(idPlanta);
            if (planta == null) return RedirectToAction(nameof(Index));

            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["MensajeError"] = "El nombre de la especie es obligatorio.";
                return RedirectToAction(nameof(Index));
            }

            if (!TryParsearUmbrales(
                temperaturaMinima, temperaturaOptima, temperaturaMaxima,
                humedadMinima, humedadOptima, humedadMaxima,
                out decimal tMin, out decimal tOpt, out decimal tMax,
                out decimal hMin, out decimal hOpt, out decimal hMax,
                out string error))
            {
                TempData["MensajeError"] = error;
                return RedirectToAction(nameof(Index));
            }

            string estadoAnterior = $"{planta.Nombre} (Temp: {planta.TemperaturaMinima}/{planta.TemperaturaOptima}/{planta.TemperaturaMaxima}°C, Hum: {planta.HumedadMinima}/{planta.HumedadOptima}/{planta.HumedadMaxima}%)";

            planta.Nombre = nombre.Trim();
            planta.Descripcion = descripcion?.Trim();
            planta.TemperaturaMinima = tMin;
            planta.TemperaturaOptima = tOpt;
            planta.TemperaturaMaxima = tMax;
            planta.HumedadMinima = hMin;
            planta.HumedadOptima = hOpt;
            planta.HumedadMaxima = hMax;
            planta.FechaActualizacion = DateTime.Now;

            await _db.SaveChangesAsync();

            string estadoNuevo = $"{planta.Nombre} (Temp: {tMin}/{tOpt}/{tMax}°C, Hum: {hMin}/{hOpt}/{hMax}%)";

            await _auditoria.RegistrarAsync(
                "EDITAR_PLANTA",
                "Plantas",
                $"ANTES: {estadoAnterior}  --->  DESPUÉS: {estadoNuevo}"
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
            string sTMin, string? sTOpt, string sTMax,
            string sHMin, string? sHOpt, string sHMax,
            out decimal tMin, out decimal tOpt, out decimal tMax,
            out decimal hMin, out decimal hOpt, out decimal hMax,
            out string error)
        {
            tMin = tOpt = tMax = hMin = hOpt = hMax = 0;
            error = string.Empty;

            bool ok1 = decimal.TryParse(sTMin?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out tMin);
            bool ok3 = decimal.TryParse(sTMax?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out tMax);
            bool ok4 = decimal.TryParse(sHMin?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out hMin);
            bool ok6 = decimal.TryParse(sHMax?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out hMax);

            if (!ok1 || !ok3 || !ok4 || !ok6)
            {
                error = "Formato numérico inválido en los umbrales.";
                return false;
            }

            tMin = Math.Round(tMin, 2);
            tMax = Math.Round(tMax, 2);
            hMin = Math.Round(hMin, 2);
            hMax = Math.Round(hMax, 2);

            if (tMin < -10 || tMax > 60 || tMin >= tMax)
            {
                error = "La temperatura debe estar entre -10°C y 60°C, y la mínima debe ser menor que la máxima.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sTOpt))
            {
                tOpt = Math.Round((tMin + tMax) / 2m, 2);
            }
            else if (!decimal.TryParse(sTOpt.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out tOpt))
            {
                error = "El inicio del rango óptimo de temperatura tiene un formato inválido.";
                return false;
            }

            tOpt = Math.Round(tOpt, 2);
            if (tOpt < tMin || tOpt >= tMax)
            {
                error = "El inicio del rango óptimo de temperatura debe ser mayor o igual al mínimo y menor que el máximo.";
                return false;
            }

            if (hMin < 0 || hMax > 100 || hMin >= hMax)
            {
                error = "La humedad debe estar entre 0% y 100%, y la mínima debe ser menor que la máxima.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sHOpt))
            {
                hOpt = Math.Round((hMin + hMax) / 2m, 2);
            }
            else if (!decimal.TryParse(sHOpt.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out hOpt))
            {
                error = "El inicio del rango óptimo de humedad tiene un formato inválido.";
                return false;
            }

            hOpt = Math.Round(hOpt, 2);
            if (hOpt < hMin || hOpt >= hMax)
            {
                error = "El inicio del rango óptimo de humedad debe ser mayor o igual al mínimo y menor que el máximo.";
                return false;
            }

            return true;
        }
    }
}
