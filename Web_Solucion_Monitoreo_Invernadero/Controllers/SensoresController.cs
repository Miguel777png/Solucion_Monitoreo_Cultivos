using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [VerificarAcceso(SoloAdministrador = true)]
    public class SensoresController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        public SensoresController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        public async Task<IActionResult> Index(string? busqueda, string? estadoFiltro)
        {
            ViewBag.Busqueda = busqueda;
            ViewBag.EstadoFiltro = estadoFiltro;
            ViewBag.Invernaderos = await _db.Invernaderos
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            var consulta = _db.Sensores
                .Include(s => s.Invernadero)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                string term = busqueda.Trim();
                consulta = consulta.Where(s =>
                    s.CodigoDispositivo.Contains(term) ||
                    s.NombreNodo.Contains(term));
            }

            if (estadoFiltro == "disponible")
                consulta = consulta.Where(s => s.State && s.IdInvernadero == null);
            else if (estadoFiltro == "asignado")
                consulta = consulta.Where(s => s.State && s.IdInvernadero != null);
            else if (estadoFiltro == "inactivo")
                consulta = consulta.Where(s => !s.State);

            var sensores = await consulta
                .OrderBy(s => s.IdSensor)
                .ToListAsync();

            return View(sensores);
        }

        // REGISTRAR CÓDIGO HARDWARE EN LISTA BLANCA (OPCIONALMENTE ASIGNADO O DISPONIBLE)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrar(
            string codigoDispositivo,
            string? nombreNodo,
            int? idInvernadero)
        {
            if (string.IsNullOrWhiteSpace(codigoDispositivo))
            {
                TempData["MensajeError"] = "El código de dispositivo del ESP32 es obligatorio.";
                return RedirectToAction(nameof(Index));
            }

            string codigoLimpio = codigoDispositivo.Trim().ToUpper();

            if (await _db.Sensores.AnyAsync(s => s.CodigoDispositivo == codigoLimpio))
            {
                TempData["MensajeError"] = $"El código '{codigoLimpio}' ya está registrado en la lista blanca.";
                return RedirectToAction(nameof(Index));
            }

            Invernadero? inv = null;
            if (idInvernadero.HasValue && idInvernadero.Value > 0)
            {
                inv = await _db.Invernaderos.FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero.Value && i.State);
            }

            var nuevo = new Sensor
            {
                CodigoDispositivo = codigoLimpio,
                NombreNodo = string.IsNullOrWhiteSpace(nombreNodo) ? $"Nodo {codigoLimpio}" : nombreNodo.Trim(),
                IdInvernadero = inv?.IdInvernadero,
                EstadoConexion = "Desconectado",
                State = true
            };

            _db.Sensores.Add(nuevo);
            await _db.SaveChangesAsync();

            string destinoTexto = inv != null ? $"asignado a '{inv.Nombre}'" : "en estado Disponible (sin asignar)";
            await _auditoria.RegistrarAsync(
                "REGISTRAR_SENSOR_WHITELIST",
                "Sensores IoT",
                $"Registró código permitido '{codigoLimpio}' ({nuevo.NombreNodo}) {destinoTexto}."
            );

            TempData["MensajeExito"] = $"Dispositivo '{codigoLimpio}' agregado a la lista blanca.";
            return RedirectToAction(nameof(Index));
        }

        // ASIGNAR O DESVINCULAR UN SENSOR DE UN INVERNADERO
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarAsignacion(int idSensor, int? idInvernadero)
        {
            var sensor = await _db.Sensores
                .Include(s => s.Invernadero)
                .FirstOrDefaultAsync(s => s.IdSensor == idSensor);

            if (sensor == null) return RedirectToAction(nameof(Index));

            string antes = sensor.Invernadero?.Nombre ?? "Disponible (Sin asignar)";
            Invernadero? nuevoInv = null;

            if (idInvernadero.HasValue && idInvernadero.Value > 0)
            {
                nuevoInv = await _db.Invernaderos.FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero.Value && i.State);
            }

            sensor.IdInvernadero = nuevoInv?.IdInvernadero;
            await _db.SaveChangesAsync();

            string despues = nuevoInv?.Nombre ?? "Disponible (Sin asignar)";
            await _auditoria.RegistrarAsync(
                "ASIGNACION_SENSOR",
                "Sensores IoT",
                $"Sensor '{sensor.CodigoDispositivo}' | ANTES: {antes}  --->  DESPUÉS: {despues}"
            );

            TempData["MensajeExito"] = $"Asignación de '{sensor.CodigoDispositivo}' actualizada.";
            return RedirectToAction(nameof(Index));
        }

        // ACTIVAR / DESACTIVAR EN LISTA BLANCA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarEstado(int idSensor)
        {
            var sensor = await _db.Sensores.FindAsync(idSensor);
            if (sensor == null) return RedirectToAction(nameof(Index));

            bool estadoPrevio = sensor.State;
            sensor.State = !sensor.State;
            if (!sensor.State) sensor.EstadoConexion = "Inactivo";

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "ESTADO_SENSOR",
                "Sensores IoT",
                $"Sensor '{sensor.CodigoDispositivo}' | ANTES: {(estadoPrevio ? "Activo" : "Inactivo")}  --->  DESPUÉS: {(sensor.State ? "Activo" : "Inactivo")}"
            );

            TempData["MensajeExito"] = $"Estado de '{sensor.CodigoDispositivo}' actualizado.";
            return RedirectToAction(nameof(Index));
        }
    }
}