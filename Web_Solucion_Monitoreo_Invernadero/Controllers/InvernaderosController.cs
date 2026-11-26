using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{

    /// <summary>
    /// Administra los invernaderos, sus plantas y sensores asignados.
    /// </summary>
    [VerificarAcceso]
    public class InvernaderosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        /// <summary>
        /// Inicializa el controlador de invernaderos.
        /// </summary>
        /// <param name="db">Contexto de la base de datos.</param>
        /// <param name="auditoria">Servicio que registra los cambios.</param>
        public InvernaderosController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        /// <summary>
        /// Muestra los invernaderos y el detalle del seleccionado.
        /// </summary>
        /// <param name="idSeleccionado">Identificador del invernadero seleccionado.</param>
        /// <returns>La vista principal de invernaderos.</returns>
        public async Task<IActionResult> Index(int? idSeleccionado)
        {
            var invernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Include(i => i.Sensores)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            var seleccionado = idSeleccionado.HasValue
                ? invernaderos.FirstOrDefault(i => i.IdInvernadero == idSeleccionado.Value)
                : invernaderos.FirstOrDefault();

            seleccionado ??= invernaderos.FirstOrDefault();

            // Plantas activas libres (no asignadas a otro invernadero activo)
            var plantasDisponibles = await _db.Plantas
                .Include(p => p.Invernadero)
                .Where(p => p.State && (p.Invernadero == null || !p.Invernadero.State))
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            // Sensores activos en lista blanca sin invernadero asignado
            var sensoresDisponibles = await _db.Sensores
                .Where(s => s.State && s.IdInvernadero == null)
                .OrderBy(s => s.CodigoDispositivo)
                .ToListAsync();

            ViewBag.Seleccionado = seleccionado;
            ViewBag.PlantasDisponibles = plantasDisponibles;
            ViewBag.SensoresDisponibles = sensoresDisponibles;
            ViewBag.EsAdministrador = User.IsInRole("Administrador");

            return View(invernaderos);
        }

        /// <summary>
        /// Actualiza la planta y la frecuencia de registro de un invernadero.
        /// </summary>
        /// <param name="idInvernadero">Identificador del invernadero.</param>
        /// <param name="idNuevaPlanta">Nueva planta que se desea asignar.</param>
        /// <param name="frecuenciaRegistroMin">Frecuencia de guardado en minutos.</param>
        /// <returns>Una redirección al invernadero actualizado.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarConfiguracionOperativa(
            int idInvernadero,
            int? idNuevaPlanta,
            int frecuenciaRegistroMin)
        {
            var invernadero = await _db.Invernaderos
                .Include(i => i.Planta)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero && i.State);

            if (invernadero == null)
            {
                TempData["MensajeError"] = "Invernadero no encontrado.";
                return RedirectToAction(nameof(Index));
            }

            int nuevaFrecuencia = frecuenciaRegistroMin > 0 ? frecuenciaRegistroMin : 60;
            string plantaAntes = invernadero.Planta?.Nombre ?? "Sin especie";
            int freqAntes = invernadero.FrecuenciaRegistroMin;

            string plantaDespues = plantaAntes;

            // Si seleccionó cambiar de planta, valida regla 1:1
            if (idNuevaPlanta.HasValue && idNuevaPlanta.Value > 0 && idNuevaPlanta.Value != invernadero.IdPlanta)
            {
                var nuevaPlanta = await _db.Plantas
                    .Include(p => p.Invernadero)
                    .FirstOrDefaultAsync(p => p.IdPlanta == idNuevaPlanta.Value && p.State);

                if (nuevaPlanta == null)
                {
                    TempData["MensajeError"] = "La especie seleccionada no es válida.";
                    return RedirectToAction(nameof(Index), new { idSeleccionado = idInvernadero });
                }

                if (nuevaPlanta.Invernadero != null &&
                    nuevaPlanta.Invernadero.State &&
                    nuevaPlanta.Invernadero.IdInvernadero != idInvernadero)
                {
                    TempData["MensajeError"] = $"La especie '{nuevaPlanta.Nombre}' ya está asignada a '{nuevaPlanta.Invernadero.Nombre}'.";
                    return RedirectToAction(nameof(Index), new { idSeleccionado = idInvernadero });
                }

                invernadero.IdPlanta = nuevaPlanta.IdPlanta;
                plantaDespues = nuevaPlanta.Nombre;
            }

            invernadero.FrecuenciaRegistroMin = nuevaFrecuencia;
            invernadero.IdUsuarioModificacion = HttpContext.Session.GetInt32("IdUsuario") ?? 1;
            invernadero.FechaModificacion = DateTime.Now;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "CONFIGURAR_INVERNADERO",
                invernadero.Nombre,
                $"ANTES: Especie '{plantaAntes}', Guardado cada {freqAntes} min  --->  DESPUÉS: Especie '{plantaDespues}', Guardado cada {nuevaFrecuencia} min"
            );

            TempData["MensajeExito"] = $"Configuración de '{invernadero.Nombre}' actualizada correctamente.";
            return RedirectToAction(nameof(Index), new { idSeleccionado = idInvernadero });
        }

        /// <summary>
        /// Crea un nuevo invernadero. Esta acción es solo para administradores.
        /// </summary>
        /// <param name="nombre">Nombre del nuevo invernadero.</param>
        /// <param name="ubicacion">Ubicación o sector.</param>
        /// <param name="idPlanta">Planta que se asignará inicialmente.</param>
        /// <param name="frecuenciaRegistroMin">Frecuencia de guardado en minutos.</param>
        /// <returns>Una redirección al nuevo invernadero.</returns>
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
                TempData["MensajeError"] = "Debes elegir una especie disponible del catálogo (no asignada a otro sector).";
                return RedirectToAction(nameof(Index));
            }

            int frecuencia = frecuenciaRegistroMin > 0 ? frecuenciaRegistroMin : 60;
            int idUsuario = HttpContext.Session.GetInt32("IdUsuario") ?? 1;

            var nuevoInv = new Invernadero
            {
                Nombre = nombre.Trim(),
                Ubicacion = string.IsNullOrWhiteSpace(ubicacion) ? "Sector General" : ubicacion.Trim(),
                IdPlanta = planta.IdPlanta,
                FrecuenciaRegistroMin = frecuencia,
                IdUsuarioModificacion = idUsuario,
                State = true
            };

            _db.Invernaderos.Add(nuevoInv);
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "CREAR_INVERNADERO",
                nuevoInv.Nombre,
                $"Creó sector '{nuevoInv.Nombre}' ({nuevoInv.Ubicacion}) con especie '{planta.Nombre}' y guardado cada {frecuencia} min."
            );

            TempData["MensajeExito"] = $"Invernadero '{nuevoInv.Nombre}' creado correctamente.";
            return RedirectToAction(nameof(Index), new { idSeleccionado = nuevoInv.IdInvernadero });
        }

        /// <summary>
        /// Asigna un sensor disponible a un invernadero.
        /// </summary>
        /// <param name="idInvernadero">Identificador del invernadero.</param>
        /// <param name="idSensor">Identificador del sensor disponible.</param>
        /// <returns>Una redirección al invernadero actualizado.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [VerificarAcceso(SoloAdministrador = true)]
        public async Task<IActionResult> AsignarSensorDisponible(int idInvernadero, int idSensor)
        {
            var invernadero = await _db.Invernaderos
                .Include(i => i.Sensores)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero && i.State);

            var sensorNuevo = await _db.Sensores
                .FirstOrDefaultAsync(s => s.IdSensor == idSensor && s.State && s.IdInvernadero == null);

            if (invernadero == null || sensorNuevo == null)
            {
                TempData["MensajeError"] = "El sensor seleccionado ya no está disponible o el invernadero no es válido.";
                return RedirectToAction(nameof(Index), new { idSeleccionado = idInvernadero });
            }

            // Si el invernadero ya tenía un sensor previo, lo libera a estado Disponible
            var sensorPrevio = invernadero.Sensores.FirstOrDefault();
            string sensorAntes = sensorPrevio?.CodigoDispositivo ?? "Sin sensor asignado";

            if (sensorPrevio != null)
            {
                sensorPrevio.IdInvernadero = null;
                sensorPrevio.EstadoConexion = "Desconectado";
            }

            sensorNuevo.IdInvernadero = idInvernadero;
            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "VINCULAR_SENSOR_INVERNADERO",
                invernadero.Nombre,
                $"Sensor vinculado | ANTES: {sensorAntes}  --->  DESPUÉS: {sensorNuevo.CodigoDispositivo}"
            );

            TempData["MensajeExito"] = $"Dispositivo '{sensorNuevo.CodigoDispositivo}' vinculado a '{invernadero.Nombre}'.";
            return RedirectToAction(nameof(Index), new { idSeleccionado = idInvernadero });
        }
    }
}
