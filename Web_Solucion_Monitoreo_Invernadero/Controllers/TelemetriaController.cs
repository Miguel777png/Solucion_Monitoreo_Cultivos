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
            estadoRam.HumedadMaxima = planta.HumedadMaxima;
            estadoRam.HumedadMinima = planta.HumedadMinima;
            estadoRam.ConfiguracionCargada = true;

            bool huboCambiosBd = false;

            if (sensor.EstadoConexion != "Conectado")
            {
                sensor.EstadoConexion = "Conectado";
                huboCambiosBd = true;
            }
            sensor.UltimaSenal = DateTime.Now;

            estadoRam.RegistrarLectura(datos.Humedad, datos.Temperatura);

            // Cada variable ambiental controla un actuador distinto. La temperatura
            // nunca activa la regadera: alta = ventilación, baja = calefacción.
            bool humedadBaja = datos.Humedad < estadoRam.HumedadMinima;
            bool humedadAlta = datos.Humedad > estadoRam.HumedadMaxima;
            bool activarVentilacion = datos.Temperatura > estadoRam.TemperaturaMaxima;
            bool activarCalefaccion = datos.Temperatura < estadoRam.TemperaturaMinima;

            bool activarRegadera = false;
            int segundosAEncender = 0;
            DateTime ahora = DateTime.Now;

            // Evita superponer pulsos de emergencia de tres minutos. Una alerta de
            // temperatura no altera este temporizador ni puede impedir un riego.
            if (humedadBaja &&
                (ahora - estadoRam.UltimoRiegoEmergencia).TotalSeconds >= 180)
            {
                activarRegadera = true;
                segundosAEncender = 180;
                estadoRam.UltimoRiegoEmergencia = ahora;

                string climatizacion = activarVentilacion
                    ? " Ventilación activada por temperatura alta."
                    : activarCalefaccion
                        ? " Calefacción activada por temperatura baja."
                        : string.Empty;

                _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = idInv,
                    TipoEvento = "RIEGO_EMERGENCIA",
                    Descripcion = $"[{planta.Nombre}] Humedad baja ({datos.Humedad}% < {planta.HumedadMinima}%): riego de emergencia 3 min (180s).{climatizacion}",
                    TiempoActivacionSeg = 180,
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
                    condiciones.Add($"temperatura alta ({datos.Temperatura}°C > {planta.TemperaturaMaxima}°C): ventilación activada");

                if (activarCalefaccion)
                    condiciones.Add($"temperatura baja ({datos.Temperatura}°C < {planta.TemperaturaMinima}°C): calefacción activada");

                _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = idInv,
                    TipoEvento = "ALERTA_UMBRAL",
                    Descripcion = $"[{planta.Nombre}] {string.Join("; ", condiciones)}. Sin riego por temperatura.",
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

            // El riego programado solo se entrega si no se acaba de ordenar un
            // riego de emergencia. En caso contrario queda cubierto por esos 180s.
            if (estadoRam.RiegoProgramadoPendiente)
            {
                if (!activarRegadera)
                {
                    activarRegadera = true;
                    segundosAEncender = estadoRam.SegundosRiegoProgramado;
                }

                estadoRam.RiegoProgramadoPendiente = false;
            }

            return Ok(new
            {
                activarRegadera = activarRegadera,
                tiempoSeg = segundosAEncender,
                activarVentilacion = activarVentilacion,
                activarCalefaccion = activarCalefaccion,
                temperaturaMinima = planta.TemperaturaMinima,
                temperaturaMaxima = planta.TemperaturaMaxima
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

            if (inv?.Planta != null && estado.SensorConectado)
            {
                var p = inv.Planta;
                humedadOptima = (p.HumedadMinima + p.HumedadMaxima) / 2m;

                bool fueraRango = estado.UltimaHumedad < p.HumedadMinima ||
                                  estado.UltimaHumedad > p.HumedadMaxima ||
                                  estado.UltimaTemperatura < p.TemperaturaMinima ||
                                  estado.UltimaTemperatura > p.TemperaturaMaxima;

                bool enMargenMinimo = estado.UltimaHumedad >= p.HumedadMinima && estado.UltimaHumedad < humedadOptima;

                if (fueraRango)
                    estadoSalud = "PELIGRO";
                else if (enMargenMinimo)
                    estadoSalud = "PRECAUCION";
                else
                    estadoSalud = "OPTIMO";
            }

            return Ok(new
            {
                idInvernadero = idInvernadero,
                conectado = estado.SensorConectado,
                humedadActual = estado.UltimaHumedad,
                temperaturaActual = estado.UltimaTemperatura,
                humedadOptima = humedadOptima,
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
                    decimal hOptima = (p.HumedadMinima + p.HumedadMaxima) / 2m;

                    if (est.UltimaHumedad < p.HumedadMinima || est.UltimaHumedad > p.HumedadMaxima ||
                        est.UltimaTemperatura < p.TemperaturaMinima || est.UltimaTemperatura > p.TemperaturaMaxima)
                    {
                        salud = "PELIGRO";
                    }
                    else if (est.UltimaHumedad < hOptima)
                    {
                        salud = "PRECAUCION";
                    }
                    else
                    {
                        salud = "OPTIMO";
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
