using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{

    /// Controlador exclusivo para administradores que gestiona la lista blanca de nodos IoT (ESP32).
    /// Permite registrar códigos de hardware únicos, vincular o desvincular dispositivos garantizando
    /// la relación 1:1 con cada invernadero, y activar o desactivar nodos con registro en auditoría.
    /// </summary>

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

            var todosInvernaderos = await _db.Invernaderos
                .Include(i => i.Sensores)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            ViewBag.Invernaderos = todosInvernaderos;

            // Solo invernaderos que NO tienen ningún sensor asignado actualmente (Regla 1:1)
            ViewBag.InvernaderosLibres = todosInvernaderos
                .Where(i => !i.Sensores.Any())
                .ToList();

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

        // VALIDACIÓN CENTRALIZADA 1:1: Verifica si un invernadero ya tiene un ESP32 asignado
        private async Task<Sensor?> ObtenerSensorOcupandoInvernaderoAsync(int idInvernadero, int? excluirIdSensor = null)
        {
            var consulta = _db.Sensores.Where(s => s.IdInvernadero == idInvernadero);

            if (excluirIdSensor.HasValue)
            {
                consulta = consulta.Where(s => s.IdSensor != excluirIdSensor.Value);
            }

            return await consulta.FirstOrDefaultAsync();
        }

        // REGISTRAR CÓDIGO HARDWARE EN LISTA BLANCA
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
                if (inv == null)
                {
                    TempData["MensajeError"] = "El invernadero seleccionado no existe.";
                    return RedirectToAction(nameof(Index));
                }

                // Evita el choque UNIQUE en MySQL antes de guardar
                var sensorExistente = await ObtenerSensorOcupandoInvernaderoAsync(inv.IdInvernadero);
                if (sensorExistente != null)
                {
                    TempData["MensajeError"] = $"No se puede asignar: '{inv.Nombre}' ya tiene vinculado el dispositivo '{sensorExistente.CodigoDispositivo}'. Déjalo Disponible o libera ese invernadero primero.";
                    return RedirectToAction(nameof(Index));
                }
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
                if (nuevoInv == null)
                {
                    TempData["MensajeError"] = "El invernadero destino no es válido.";
                    return RedirectToAction(nameof(Index));
                }

                // Evita el choque UNIQUE si otro sensor ya ocupa ese invernadero
                var ocupante = await ObtenerSensorOcupandoInvernaderoAsync(nuevoInv.IdInvernadero, sensor.IdSensor);
                if (ocupante != null)
                {
                    TempData["MensajeError"] = $"No se puede mover '{sensor.CodigoDispositivo}' a '{nuevoInv.Nombre}' porque ya está ocupado por '{ocupante.CodigoDispositivo}'. Pasa primero '{ocupante.CodigoDispositivo}' a Disponible.";
                    return RedirectToAction(nameof(Index));
                }
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