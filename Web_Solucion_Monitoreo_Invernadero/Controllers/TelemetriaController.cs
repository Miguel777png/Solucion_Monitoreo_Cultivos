using Microsoft.AspNetCore.Mvc;
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



        // 4. ENDPOINT DE ALERTAS EN VIVO PARA EL PANEL DERECHO DEL DASHBOARD
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
                    tipoEvento = b.TipoEvento,
                    descripcion = b.Descripcion,
                    fechaHora = b.FechaHora.ToString("dd/MM HH:mm:ss")
                })
                .ToListAsync();

            return Ok(alertas);
        }



        // 1. ENDPOINT PARA LOS 3 ESP32: POST /api/telemetria
        [HttpPost]
        public async Task<IActionResult> RecibirLectura([FromBody] LecturaSensorDto datos)
        {
            // Filtra por lista blanca: solo entran ESP32-ORQ-01, ESP32-ORQ-02 o ESP32-ORQ-03 activos
            var sensor = await _db.Sensores
                .Include(s => s.Invernadero)
                    .ThenInclude(i => i!.Planta)
                .FirstOrDefaultAsync(s => s.CodigoDispositivo == datos.CodigoDispositivo && s.State);

            if (sensor == null || sensor.Invernadero == null || sensor.Invernadero.Planta == null)
            {
                return NotFound(new { mensaje = $"Dispositivo '{datos.CodigoDispositivo}' no registrado o inactivo." });
            }

            int idInv = sensor.IdInvernadero;
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

            // Evaluación de las 4 reglas según la planta del invernadero
            bool necesitaRiegoEmergencia = datos.Humedad < estadoRam.HumedadMinima || datos.Temperatura > estadoRam.TemperaturaMaxima;
            bool alertaSinRiego = datos.Humedad > estadoRam.HumedadMaxima || datos.Temperatura < estadoRam.TemperaturaMinima;
            bool activarRegadera = false;

            if (necesitaRiegoEmergencia || alertaSinRiego)
            {
                if ((DateTime.Now - estadoRam.UltimaAlertaCritica).TotalMinutes >= 5)
                {
                    estadoRam.UltimaAlertaCritica = DateTime.Now;

                    if (necesitaRiegoEmergencia)
                    {
                        activarRegadera = true;
                        _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                        {
                            IdInvernadero = idInv,
                            TipoEvento = "RIEGO_EMERGENCIA",
                            Descripcion = $"[{planta.Nombre}] Umbral crítico (Riego activado): Humedad {datos.Humedad}% / Temp {datos.Temperatura}°C",
                            TiempoActivacionSeg = 5,
                            HumedadMomento = datos.Humedad,
                            TemperaturaMomento = datos.Temperatura,
                            FechaHora = DateTime.Now,
                            Leida = false
                        });
                    }
                    else
                    {
                        _db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                        {
                            IdInvernadero = idInv,
                            TipoEvento = "ALERTA_UMBRAL",
                            Descripcion = $"[{planta.Nombre}] Fuera de rango óptimo (Sin riego): Humedad {datos.Humedad}% / Temp {datos.Temperatura}°C",
                            TiempoActivacionSeg = 0,
                            HumedadMomento = datos.Humedad,
                            TemperaturaMomento = datos.Temperatura,
                            FechaHora = DateTime.Now,
                            Leida = false
                        });
                    }

                    huboCambiosBd = true;
                }
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

            int segundosAEncender = activarRegadera ? 5 : 0;

            if (estadoRam.RiegoProgramadoPendiente)
            {
                activarRegadera = true;
                segundosAEncender = estadoRam.SegundosRiegoProgramado;
                estadoRam.RiegoProgramadoPendiente = false;
            }

            return Ok(new
            {
                activarRegadera = activarRegadera,
                tiempoSeg = segundosAEncender
            });
        }

        // 2. ENDPOINT EN VIVO DE UN INVERNADERO ESPECÍFICO: GET /api/telemetria/actual/1
        [HttpGet("actual/{idInvernadero:int}")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult ObtenerEnVivo(int idInvernadero)
        {
            var estado = _ram.ObtenerOCrear(idInvernadero);

            return Ok(new
            {
                idInvernadero = idInvernadero,
                conectado = estado.SensorConectado,
                humedadActual = estado.UltimaHumedad,
                temperaturaActual = estado.UltimaTemperatura,
                ultimaHora = estado.UltimaActualizacion == DateTime.MinValue
                    ? "Sin datos"
                    : estado.UltimaActualizacion.ToString("HH:mm:ss"),
                historialReciente = estado.ObtenerPuntos()
            });
        }

        // 3. ENDPOINT GLOBAL PARA LAS 3 TARJETAS DEL DASHBOARD: GET /api/telemetria/resumen-todos
        [HttpGet("resumen-todos")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult ObtenerResumenTodos()
        {
            var resumen = new[] { 1, 2, 3 }.Select(id =>
            {
                var est = _ram.ObtenerOCrear(id);
                return new
                {
                    idInvernadero = id,
                    conectado = est.SensorConectado,
                    humedad = est.UltimaHumedad,
                    temperatura = est.UltimaTemperatura
                };
            });

            return Ok(resumen);
        }
    }
}