using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelemetriaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly EstadoInvernaderoService _ram;

        public TelemetriaController(ApplicationDbContext db, EstadoInvernaderoService ram)
        {
            _db = db;
            _ram = ram;
        }

        // 4. ENDPOINT DE ALERTAS EN VIVO PARA EL PANEL DERECHO Y TOASTS DEL DASHBOARD
        [HttpGet("alertas/{idInvernadero:int}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerAlertasRecientes(int idInvernadero)
        {
            var alertas = await _db.BitacoraRiegoAlertas
                .Where(b => b.IdInvernadero == idInvernadero)
                .OrderByDescending(b => b.FechaHora)
                .Take(8)
                .Select(b => new
                {
                    idBitacora = b.IdBitacora,
                    tipoEvento = b.TipoEvento,
                    descripcion = b.Descripcion,
                    tiempoSeg = b.TiempoActivacionSeg,
                    fechaHora = b.FechaHora.ToString("dd/MM HH:mm:ss")
                })
                .ToListAsync();

            return Ok(alertas);
        }

        // 1. ENDPOINT PARA LOS ESP32: POST /api/telemetria
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RecibirLectura([FromBody] LecturaSensorDto datos)
        {
            var sensor = await _db.Sensores
                .Include(s => s.Invernadero)
                    .ThenInclude(i => i!.Planta)
                .FirstOrDefaultAsync(s => s.CodigoDispositivo == datos.CodigoDispositivo && s.State);

            if (sensor == null || sensor.Invernadero == null || sensor.Invernadero.Planta == null)
            {
                return NotFound(new { mensaje = $"Dispositivo '{datos.CodigoDispositivo}' no registrado o inactivo." });
            }

            int idInv = sensor.IdInvernadero!.Value;
            var estadoRam = _ram.ObtenerOCrear(idInv);
            var planta = sensor.Invernadero.Planta;

            // Sincroniza en RAM los umbrales de la planta única asignada a este invernadero
            estadoRam.FrecuenciaRegistroMin = sensor.Invernadero.FrecuenciaRegistroMin;
            estadoRam.TemperaturaMaxima = planta.TemperaturaMaxima;
            estadoRam.TemperaturaMinima = planta.TemperaturaMinima;
            estadoRam.TemperaturaOptima = planta.TemperaturaOptima;
            estadoRam.HumedadMaxima = planta.HumedadMaxima;
            estadoRam.HumedadMinima = planta.HumedadMinima;
            estadoRam.HumedadOptima = planta.HumedadOptima;
            estadoRam.ConfiguracionCargada = true;

            bool huboCambiosBd = false;

            if (sensor.EstadoConexion != "Conectado")
            {
                sensor.EstadoConexion = "Conectado";
                huboCambiosBd = true;
            }
            sensor.UltimaSenal = DateTime.Now;

            estadoRam.RegistrarLectura(datos.Humedad, datos.Temperatura);
            estadoRam.ActualizarClimatizacion(datos.Temperatura);
            bool cambioRiegoEmergencia = estadoRam.ActualizarRiegoEmergencia(datos.Humedad);

            // Cada variable ambiental controla un actuador distinto. La temperatura
            // nunca activa la regadera: alta = ventilación, baja = calefacción.
            bool humedadAlta = datos.Humedad > estadoRam.HumedadMaxima;
            bool activarVentilacion = estadoRam.VentilacionActiva;
            bool activarCalefaccion = estadoRam.CalefaccionActiva;

            bool activarRegadera = estadoRam.RiegoEmergenciaActivo;
            int segundosAEncender = 0;
            DateTime ahora = DateTime.Now;

            // El riego de emergencia es continuo: abre al bajar del mínimo y el
            // ESP32 lo cierra cuando una lectura vuelve al rango permitido.
            if (cambioRiegoEmergencia && activarRegadera)
            {
                string climatizacion = activarVentilacion
                    ? " Ventilación activada por temperatura alta."
                    : activarCalefaccion
                        ? " Calefacción activada por temperatura baja."
                        : string.Empty;

                _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = idInv,
                    TipoEvento = "RIEGO_EMERGENCIA",
                    Descripcion = $"[{planta.Nombre}] Humedad baja ({datos.Humedad}% < {planta.HumedadMinima}%): válvula abierta hasta recuperar el rango permitido.{climatizacion}",
                    TiempoActivacionSeg = 0,
                    HumedadMomento = datos.Humedad,
                    TemperaturaMomento = datos.Temperatura,
                    FechaHora = ahora,
                    Leida = false
                });

                huboCambiosBd = true;
            }
            else if (cambioRiegoEmergencia && !activarRegadera)
            {
                _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = idInv,
                    TipoEvento = "RIEGO_EMERGENCIA_FINALIZADO",
                    Descripcion = $"[{planta.Nombre}] Humedad recuperada ({datos.Humedad}%): válvula cerrada al volver al rango {planta.HumedadMinima}%–{planta.HumedadMaxima}%.",
                    TiempoActivacionSeg = 0,
                    HumedadMomento = datos.Humedad,
                    TemperaturaMomento = datos.Temperatura,
                    FechaHora = ahora,
                    Leida = false
                });

                huboCambiosBd = true;
            }

            // Registra las condiciones que no requieren agua sin repetir la alerta
            // en cada lectura del ESP32.
            bool existeAlertaSinRiego = humedadAlta || activarVentilacion || activarCalefaccion;
            if (existeAlertaSinRiego &&
                (ahora - estadoRam.UltimaAlertaUmbral).TotalMinutes >= 1)
            {
                var condiciones = new List<string>();

                if (humedadAlta)
                    condiciones.Add($"humedad alta ({datos.Humedad}% > {planta.HumedadMaxima}%)");

                if (activarVentilacion)
                    condiciones.Add($"ventilación activa hasta regresar a {planta.TemperaturaMaxima}°C o menos");

                if (activarCalefaccion)
                    condiciones.Add($"calefacción activa hasta regresar a {planta.TemperaturaMinima}°C o más");

                string detalleRiego = activarVentilacion || activarCalefaccion
                    ? " Sin riego provocado por la temperatura."
                    : string.Empty;

                _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = idInv,
                    TipoEvento = "ALERTA_UMBRAL",
                    Descripcion = $"[{planta.Nombre}] {string.Join("; ", condiciones)}.{detalleRiego}",
                    TiempoActivacionSeg = 0,
                    HumedadMomento = datos.Humedad,
                    TemperaturaMomento = datos.Temperatura,
                    FechaHora = ahora,
                    Leida = false
                });

                estadoRam.UltimaAlertaUmbral = ahora;
                huboCambiosBd = true;
            }

            // Guardado histórico periódico en MySQL
            if ((DateTime.Now - estadoRam.UltimoGuardadoHistorico).TotalMinutes >= estadoRam.FrecuenciaRegistroMin)
            {
                _db.RegistroHistorico.Add(new RegistroHistorico
                {
                    IdInvernadero = idInv,
                    Humedad = datos.Humedad,
                    Temperatura = datos.Temperatura,
                    FechaHora = DateTime.Now,
                    State = true
                });

                estadoRam.UltimoGuardadoHistorico = DateTime.Now;
                huboCambiosBd = true;
            }

            if (huboCambiosBd)
            {
                await _db.SaveChangesAsync();
            }

            ComandoRiego? comandoRiego = null;

            // Una orden persistente permanece disponible hasta que el ESP32 la
            // confirme, expire o falle. La temperatura nunca crea estas órdenes.
            if (!activarRegadera)
            {
                comandoRiego = await _db.ComandosRiego
                    .Where(c =>
                        c.IdInvernadero == idInv &&
                        (c.Estado == EstadosComandoRiego.Pendiente ||
                         c.Estado == EstadosComandoRiego.Entregado) &&
                        c.FechaExpiracion > ahora)
                    .OrderByDescending(c => c.Prioridad)
                    .ThenBy(c => c.FechaCreacion)
                    .FirstOrDefaultAsync();

                if (comandoRiego != null)
                {
                    activarRegadera = true;
                    segundosAEncender = comandoRiego.DuracionSeg;

                    if (comandoRiego.Estado == EstadosComandoRiego.Pendiente)
                    {
                        comandoRiego.Estado = EstadosComandoRiego.Entregado;
                        comandoRiego.FechaEntrega = ahora;
                        await _db.SaveChangesAsync();
                    }
                }
            }

            return Ok(new
            {
                activarRegadera = activarRegadera,
                tiempoSeg = segundosAEncender,
                riegoEmergenciaContinuo = estadoRam.RiegoEmergenciaActivo,
                activarVentilacion = activarVentilacion,
                activarCalefaccion = activarCalefaccion,
                temperaturaMinima = planta.TemperaturaMinima,
                temperaturaOptima = planta.TemperaturaOptima,
                temperaturaMaxima = planta.TemperaturaMaxima,
                humedadMinima = planta.HumedadMinima,
                humedadOptima = planta.HumedadOptima,
                humedadMaxima = planta.HumedadMaxima,
                idComandoRiego = comandoRiego?.IdComandoRiego,
                claveIdempotencia = comandoRiego?.ClaveIdempotencia,
                fechaExpiracion = comandoRiego?.FechaExpiracion
            });
        }

        // El ESP32 confirma primero que comenzó la orden y luego que cerró la
        // válvula. Las confirmaciones repetidas son idempotentes.
        [HttpPost("comandos/confirmar")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmarComando(
            [FromBody] ConfirmacionComandoRiegoDto confirmacion)
        {
            string codigo = confirmacion.CodigoDispositivo.Trim().ToUpperInvariant();
            string estadoSolicitado = confirmacion.Estado.Trim();

            var sensor = await _db.Sensores
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.CodigoDispositivo == codigo &&
                    s.State &&
                    s.IdInvernadero != null);

            if (sensor?.IdInvernadero == null)
                return NotFound(new { mensaje = "Dispositivo no registrado, inactivo o sin invernadero." });

            var comando = await _db.ComandosRiego
                .FirstOrDefaultAsync(c =>
                    c.IdComandoRiego == confirmacion.IdComandoRiego &&
                    c.IdInvernadero == sensor.IdInvernadero.Value);

            if (comando == null)
                return NotFound(new { mensaje = "La orden de riego no existe para este dispositivo." });

            if (comando.Estado == EstadosComandoRiego.Expirado)
                return Conflict(new { mensaje = "La orden ya expiró.", estado = comando.Estado });

            if (comando.Estado == EstadosComandoRiego.Fallido &&
                !confirmacion.Estado.Equals(EstadosComandoRiego.Fallido, StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { mensaje = "La orden ya fue declarada fallida.", estado = comando.Estado });
            }

            DateTime ahora = DateTime.Now;
            bool agregarEventoFinal = false;
            string tipoEventoFinal = string.Empty;
            string descripcionFinal = string.Empty;

            if (estadoSolicitado.Equals(EstadosComandoRiego.Confirmado, StringComparison.OrdinalIgnoreCase))
            {
                if (comando.Estado == EstadosComandoRiego.Completado ||
                    comando.Estado == EstadosComandoRiego.Fallido)
                {
                    return Ok(new { comando.IdComandoRiego, comando.Estado });
                }

                comando.Estado = EstadosComandoRiego.Confirmado;
                comando.FechaConfirmacion ??= ahora;
            }
            else if (estadoSolicitado.Equals(EstadosComandoRiego.Completado, StringComparison.OrdinalIgnoreCase))
            {
                if (comando.Estado != EstadosComandoRiego.Completado)
                {
                    comando.Estado = EstadosComandoRiego.Completado;
                    comando.FechaConfirmacion ??= ahora;
                    comando.FechaFinalizacion = ahora;
                    agregarEventoFinal = true;
                    tipoEventoFinal = "RIEGO_COMPLETADO";
                    descripcionFinal = $"Orden #{comando.IdComandoRiego} completada y confirmada por '{codigo}' ({comando.DuracionSeg}s).";
                }
            }
            else if (estadoSolicitado.Equals(EstadosComandoRiego.Fallido, StringComparison.OrdinalIgnoreCase))
            {
                if (comando.Estado != EstadosComandoRiego.Fallido)
                {
                    comando.Estado = EstadosComandoRiego.Fallido;
                    comando.FechaFinalizacion = ahora;
                    agregarEventoFinal = true;
                    tipoEventoFinal = "RIEGO_FALLIDO";
                    descripcionFinal = $"Orden #{comando.IdComandoRiego} reportada como fallida por '{codigo}'.";
                }
            }
            else
            {
                return BadRequest(new
                {
                    mensaje = "Estado no válido. Usa Confirmado, Completado o Fallido."
                });
            }

            if (agregarEventoFinal)
            {
                var estado = _ram.ObtenerOCrear(comando.IdInvernadero);
                _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = comando.IdInvernadero,
                    TipoEvento = tipoEventoFinal,
                    Descripcion = descripcionFinal,
                    TiempoActivacionSeg = comando.Estado == EstadosComandoRiego.Completado
                        ? comando.DuracionSeg
                        : 0,
                    HumedadMomento = estado.UltimaHumedad,
                    TemperaturaMomento = estado.UltimaTemperatura,
                    FechaHora = ahora,
                    Leida = false
                });
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                comando.IdComandoRiego,
                comando.Estado,
                comando.FechaConfirmacion,
                comando.FechaFinalizacion
            });
        }

        // 2. ENDPOINT EN VIVO DE UN INVERNADERO ESPECÍFICO: GET /api/telemetria/actual/1
        [HttpGet("actual/{idInvernadero:int}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerEnVivo(int idInvernadero)
        {
            var estado = _ram.ObtenerOCrear(idInvernadero);
            var inv = await _db.Invernaderos
                .Include(i => i.Planta)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero);

            string estadoSalud = "SIN_DATOS";
            decimal humedadOptima = 0;
            decimal temperaturaOptima = 0;

            if (inv?.Planta != null && estado.SensorConectado)
            {
                var p = inv.Planta;
                humedadOptima = p.HumedadOptima;
                temperaturaOptima = p.TemperaturaOptima;

                bool fueraRango = estado.UltimaHumedad < p.HumedadMinima ||
                                  estado.UltimaHumedad > p.HumedadMaxima ||
                                  estado.UltimaTemperatura < p.TemperaturaMinima ||
                                  estado.UltimaTemperatura > p.TemperaturaMaxima;

                bool enRangoOptimo = estado.UltimaHumedad >= p.HumedadOptima &&
                                     estado.UltimaHumedad < p.HumedadMaxima &&
                                     estado.UltimaTemperatura >= p.TemperaturaOptima &&
                                     estado.UltimaTemperatura < p.TemperaturaMaxima;

                if (fueraRango)
                    estadoSalud = "PELIGRO";
                else if (enRangoOptimo)
                    estadoSalud = "OPTIMO";
                else
                    estadoSalud = "PRECAUCION";
            }

            return Ok(new
            {
                idInvernadero = idInvernadero,
                conectado = estado.SensorConectado,
                humedadActual = estado.UltimaHumedad,
                temperaturaActual = estado.UltimaTemperatura,
                humedadOptima = humedadOptima,
                temperaturaOptima = temperaturaOptima,
                estadoSalud = estadoSalud,
                ultimaHora = estado.UltimaActualizacion == DateTime.MinValue
                    ? "Sin datos"
                    : estado.UltimaActualizacion.ToString("HH:mm:ss"),
                historialReciente = estado.ObtenerPuntos()
            });
        }

        // 3. ENDPOINT GLOBAL DINÁMICO PARA N INVERNADEROS: GET /api/telemetria/resumen-todos
        [HttpGet("resumen-todos")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ObtenerResumenTodos()
        {
            var invernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            var resumen = invernaderos.Select(inv =>
            {
                var est = _ram.ObtenerOCrear(inv.IdInvernadero);
                string salud = "SIN_DATOS";

                if (inv.Planta != null && est.SensorConectado)
                {
                    var p = inv.Planta;
                    decimal hOptima = p.HumedadOptima;

                    if (est.UltimaHumedad < p.HumedadMinima || est.UltimaHumedad > p.HumedadMaxima ||
                        est.UltimaTemperatura < p.TemperaturaMinima || est.UltimaTemperatura > p.TemperaturaMaxima)
                    {
                        salud = "PELIGRO";
                    }
                    else if (est.UltimaHumedad >= hOptima && est.UltimaHumedad < p.HumedadMaxima &&
                             est.UltimaTemperatura >= p.TemperaturaOptima && est.UltimaTemperatura < p.TemperaturaMaxima)
                    {
                        salud = "OPTIMO";
                    }
                    else
                    {
                        salud = "PRECAUCION";
                    }
                }

                return new
                {
                    idInvernadero = inv.IdInvernadero,
                    conectado = est.SensorConectado,
                    humedad = est.UltimaHumedad,
                    temperatura = est.UltimaTemperatura,
                    estadoSalud = salud
                };
            });

            return Ok(resumen);
        }
    }
}
