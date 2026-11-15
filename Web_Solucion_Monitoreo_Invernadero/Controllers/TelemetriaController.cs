using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Ruta base: /api/telemetria
    public class TelemetriaController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly EstadoInvernaderoService _ram;

        public TelemetriaController(ApplicationDbContext db, EstadoInvernaderoService ram)
        {
            _db = db;
            _ram = ram;
        }

        // 1. ENDPOINT PARA EL ESP32: POST /api/telemetria
        [HttpPost]
        public async Task<IActionResult> RecibirLectura([FromBody] LecturaSensorDto datos)
        {
            // Buscamos a qué invernadero pertenece el código del ESP32 (ej. "ESP32-ORQ-01")
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

            // Sincronizamos los umbrales y frecuencia en RAM por si fueron editados desde la web
            estadoRam.FrecuenciaRegistroMin = sensor.Invernadero.FrecuenciaRegistroMin;
            estadoRam.TemperaturaMaxima = planta.TemperaturaMaxima;
            estadoRam.TemperaturaMinima = planta.TemperaturaMinima;
            estadoRam.HumedadMaxima = planta.HumedadMaxima;
            estadoRam.HumedadMinima = planta.HumedadMinima;
            estadoRam.ConfiguracionCargada = true;

            bool huboCambiosBd = false;

            // Si el sensor estaba marcado como Desconectado en BD, lo pasamos a Conectado
            if (sensor.EstadoConexion != "Conectado")
            {
                sensor.EstadoConexion = "Conectado";
                huboCambiosBd = true;
            }
            sensor.UltimaSenal = DateTime.Now;

            // Guardamos la lectura en la memoria RAM del invernadero correspondiente (cero carga a MySQL)
            estadoRam.RegistrarLectura(datos.Humedad, datos.Temperatura);

            // Evaluación de las 4 reglas de umbrales de la planta
            bool necesitaRiegoEmergencia = datos.Humedad < estadoRam.HumedadMinima || datos.Temperatura > estadoRam.TemperaturaMaxima;
            bool alertaSinRiego = datos.Humedad > estadoRam.HumedadMaxima || datos.Temperatura < estadoRam.TemperaturaMinima;
            bool activarRegadera = false;

            if (necesitaRiegoEmergencia || alertaSinRiego)
            {
                // Cooldown de 5 minutos para no inundar la bitácora con alertas repetidas
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
                            Descripcion = $"Umbral crítico (Riego activado): Humedad {datos.Humedad}% / Temp {datos.Temperatura}°C",
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
                            Descripcion = $"Fuera de rango óptimo (Sin riego): Humedad {datos.Humedad}% / Temp {datos.Temperatura}°C",
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

            // Guardado periódico en REGISTRO_HISTORICO según la frecuencia elegida por el usuario (ej. 60 min)
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

            // Calculamos el tiempo de encendido: 5s si fue por emergencia o los segundos configurados en el calendario
            int segundosAEncender = activarRegadera ? 5 : 0;

            if (estadoRam.RiegoProgramadoPendiente)
            {
                activarRegadera = true;
                segundosAEncender = estadoRam.SegundosRiegoProgramado;
                estadoRam.RiegoProgramadoPendiente = false; // Limpiamos la orden una vez entregada al ESP32
            }

            // Respondemos al ESP32 indicando si debe encender el módulo Relé y por cuántos segundos
            return Ok(new
            {
                activarRegadera = activarRegadera,
                tiempoSeg = segundosAEncender
            });
        }

        // 2. ENDPOINT PARA EL DASHBOARD WEB (Chart.js): GET /api/telemetria/actual/1
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
    }
}