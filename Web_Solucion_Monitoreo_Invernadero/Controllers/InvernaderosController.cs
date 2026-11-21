using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [VerificarAcceso]
    public class InvernaderosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        public InvernaderosController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        // VISTA PRINCIPAL (Adapta controles según el rol de la sesión)
        public async Task<IActionResult> Index()
        {
            var invernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Include(i => i.Sensores)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            // Plantas activas que NO están asignadas a otro invernadero activo (Regla 1:1)
            var plantasDisponibles = await _db.Plantas
                .Include(p => p.Invernadero)
                .Where(p => p.State && (p.Invernadero == null || !p.Invernadero.State))
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewBag.PlantasDisponibles = plantasDisponibles;
            ViewBag.EsAdministrador = (HttpContext.Session.GetString("RolUsuario") ?? "")
                .Equals("Administrador", StringComparison.OrdinalIgnoreCase);

            return View(invernaderos);
        }

        // ACCIÓN PERMITIDA PARA JARDINERO Y ADMINISTRADOR: Cambiar especie asignada (1:1)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AsignarPlanta(int idInvernadero, int idPlanta)
        {
            var invernadero = await _db.Invernaderos
                .Include(i => i.Planta)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero && i.State);

            var nuevaPlanta = await _db.Plantas
                .Include(p => p.Invernadero)
                .FirstOrDefaultAsync(p => p.IdPlanta == idPlanta && p.State);

            if (invernadero == null || nuevaPlanta == null)
            {
                TempData["MensajeError"] = "Invernadero o especie no válida.";
                return RedirectToAction(nameof(Index));
            }

            if (nuevaPlanta.Invernadero != null &&
                nuevaPlanta.Invernadero.State &&
                nuevaPlanta.Invernadero.IdInvernadero != idInvernadero)
            {
                TempData["MensajeError"] = $"La especie '{nuevaPlanta.Nombre}' ya está ocupada en '{nuevaPlanta.Invernadero.Nombre}'.";
                return RedirectToAction(nameof(Index));
            }

            string plantaAnterior = invernadero.Planta?.Nombre ?? "Ninguna";
            invernadero.IdPlanta = nuevaPlanta.IdPlanta;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "ASIGNAR_PLANTA_INVERNADERO",
                invernadero.Nombre,
                $"Cambió especie asignada de '{plantaAnterior}' a '{nuevaPlanta.Nombre}'."
            );

            TempData["MensajeExito"] = $"'{nuevaPlanta.Nombre}' asignada correctamente a {invernadero.Nombre}.";
            return RedirectToAction(nameof(Index));
        }

        // ACCIÓN EXCLUSIVA ADMINISTRADOR: Crear nuevo Invernadero
        [HttpPost]
        [ValidateAntiForgeryToken]
        [VerificarAcceso(SoloAdministrador = true)]
        public async Task<IActionResult> CrearInvernadero(
            string nombre,
            string? ubicacion,
            int idPlanta,
            int frecuenciaRegistroMin)
        {
            var planta = await _db.Plantas
                .Include(p => p.Invernadero)
                .FirstOrDefaultAsync(p => p.IdPlanta == idPlanta && p.State);

            if (planta == null || (planta.Invernadero != null && planta.Invernadero.State))
            {
                TempData["MensajeError"] = "Debes seleccionar una especie disponible del catálogo (no asignada a otro invernadero).";
                return RedirectToAction(nameof(Index));
            }

            int frecuencia = frecuenciaRegistroMin > 0 ? frecuenciaRegistroMin : 5;

            var nuevoInv = new Invernadero
            {
                Nombre = nombre.Trim(),
                Ubicacion = string.IsNullOrWhiteSpace(ubicacion) ? "Sector General" : ubicacion.Trim(),
                IdPlanta = planta.IdPlanta,
                FrecuenciaRegistroMin = frecuencia,
                State = true
            };

            _db.Invernaderos.Add(nuevoInv);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "CREAR_INVERNADERO",
                nuevoInv.Nombre,
                $"Creó invernadero '{nuevoInv.Nombre}' ({nuevoInv.Ubicacion}) con especie '{planta.Nombre}' y guardado cada {frecuencia} min."
            );

            TempData["MensajeExito"] = $"Invernadero '{nuevoInv.Nombre}' creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        // ACCIÓN EXCLUSIVA ADMINISTRADOR: Registrar nuevo nodo ESP32
        [HttpPost]
        [ValidateAntiForgeryToken]
        [VerificarAcceso(SoloAdministrador = true)]
        public async Task<IActionResult> RegistrarSensor(
            int idInvernadero,
            string codigoDispositivo,
            string nombreNodo)
        {
            if (string.IsNullOrWhiteSpace(codigoDispositivo))
            {
                TempData["MensajeError"] = "El código de dispositivo del ESP32 es obligatorio.";
                return RedirectToAction(nameof(Index));
            }

            string codigoLimpio = codigoDispositivo.Trim().ToUpper();

            bool yaExiste = await _db.Sensores.AnyAsync(s => s.CodigoDispositivo == codigoLimpio);
            if (yaExiste)
            {
                TempData["MensajeError"] = $"El código '{codigoLimpio}' ya se encuentra registrado en el sistema.";
                return RedirectToAction(nameof(Index));
            }

            var inv = await _db.Invernaderos.FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero && i.State);
            if (inv == null)
            {
                TempData["MensajeError"] = "El invernadero seleccionado no existe.";
                return RedirectToAction(nameof(Index));
            }

            var nuevoSensor = new Sensor
            {
                IdInvernadero = idInvernadero,
                CodigoDispositivo = codigoLimpio,
                NombreNodo = string.IsNullOrWhiteSpace(nombreNodo) ? $"Nodo {codigoLimpio}" : nombreNodo.Trim(),
                EstadoConexion = "Desconectado",
                State = true
            };

            _db.Sensores.Add(nuevoSensor);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "REGISTRAR_SENSOR_ESP32",
                inv.Nombre,
                $"Vinculó el dispositivo '{codigoLimpio}' ({nuevoSensor.NombreNodo}) al invernadero '{inv.Nombre}'."
            );

            TempData["MensajeExito"] = $"Sensor '{codigoLimpio}' registrado y asociado a {inv.Nombre}.";
            return RedirectToAction(nameof(Index));
        }

        // ACCIÓN EXCLUSIVA ADMINISTRADOR: Activar/Desactivar sensor en mantenimiento
        [HttpPost]
        [ValidateAntiForgeryToken]
        [VerificarAcceso(SoloAdministrador = true)]
        public async Task<IActionResult> AlternarEstadoSensor(int idSensor)
        {
            var sensor = await _db.Sensores
                .Include(s => s.Invernadero)
                .FirstOrDefaultAsync(s => s.IdSensor == idSensor);

            if (sensor != null)
            {
                sensor.State = !sensor.State;
                if (!sensor.State) sensor.EstadoConexion = "Inactivo";
                await _db.SaveChangesAsync();

                await _auditoria.RegistrarAsync(
                    sensor.State ? "ACTIVAR_SENSOR" : "DESACTIVAR_SENSOR",
                    sensor.Invernadero?.Nombre ?? "Sensores",
                    $"Cambió estado del dispositivo '{sensor.CodigoDispositivo}' a {(sensor.State ? "Activo" : "Inactivo")}."
                );

                TempData["MensajeExito"] = $"Estado del sensor '{sensor.CodigoDispositivo}' actualizado.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}