using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Services
{

    /// <summary>
    /// Revisa los horarios de riego y crea las órdenes necesarias.
    /// </summary>
    public class RiegoProgramadoWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly EstadoInvernaderoService _ram;

        /// <summary>
        /// Inicializa el proceso de riego programado.
        /// </summary>
        /// <param name="scopeFactory">Permite crear un alcance para usar la base de datos.</param>
        /// <param name="ram">Servicio con los estados actuales.</param>
        public RiegoProgramadoWorker(IServiceScopeFactory scopeFactory, EstadoInvernaderoService ram)
        {
            _scopeFactory = scopeFactory;
            _ram = ram;
        }

        /// <summary>
        /// Ejecuta la revisión de los calendarios cada 20 segundos.
        /// </summary>
        /// <param name="stoppingToken">Token usado para detener el servicio.</param>
        /// <returns>Una tarea que se mantiene activa mientras funciona la aplicación.</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await EvaluarCalendariosAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RiegoProgramadoWorker] Error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }

        /// <summary>
        /// Evalúa los horarios activos y guarda el resultado de cada uno.
        /// </summary>
        /// <param name="ct">Token de cancelación.</param>
        /// <returns>Una tarea que representa la evaluación.</returns>
        private async Task EvaluarCalendariosAsync(CancellationToken ct)
        {
            DateTime ahora = DateTime.Now;
            string claveMinutoActual = ahora.ToString("yyyy-MM-dd HH:mm");
            string diaSemanaEs = ObtenerDiaSemanaEnEspanol(ahora.DayOfWeek);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var comandosVencidos = await db.ComandosRiego
                .Where(c =>
                    (c.Estado == EstadosComandoRiego.Pendiente ||
                     c.Estado == EstadosComandoRiego.Entregado ||
                     c.Estado == EstadosComandoRiego.Confirmado) &&
                    c.FechaExpiracion <= ahora)
                .ToListAsync(ct);

            foreach (var comandoVencido in comandosVencidos)
            {
                comandoVencido.Estado = EstadosComandoRiego.Expirado;
                comandoVencido.FechaFinalizacion = ahora;

                db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = comandoVencido.IdInvernadero,
                    TipoEvento = "RIEGO_EXPIRADO",
                    Descripcion = $"Orden #{comandoVencido.IdComandoRiego} expirada sin confirmación del ESP32.",
                    TiempoActivacionSeg = 0,
                    HumedadMomento = 0,
                    TemperaturaMomento = 0,
                    FechaHora = ahora,
                    Leida = false
                });
            }

            var horariosActivos = await db.CalendarioRiego
                .Include(c => c.Invernadero)
                    .ThenInclude(i => i!.Planta)
                .Where(c => c.State && c.Invernadero != null && c.Invernadero.State && c.Invernadero.Planta != null)
                .ToListAsync(ct);

            bool huboCambiosBd = comandosVencidos.Count > 0;

            foreach (var horario in horariosActivos)
            {
                if (horario.HoraRiego.Hour != ahora.Hour || horario.HoraRiego.Minute != ahora.Minute)
                    continue;

                bool coincideFecha = horario.FechaEspecifica.HasValue &&
                                     horario.FechaEspecifica.Value == DateOnly.FromDateTime(ahora);

                bool coincideDia = !horario.FechaEspecifica.HasValue &&
                                   !string.IsNullOrWhiteSpace(horario.DiaSemana) &&
                                   horario.DiaSemana.Trim().Equals(diaSemanaEs, StringComparison.OrdinalIgnoreCase);

                if (!coincideFecha && !coincideDia)
                    continue;

                string claveIdempotencia = $"CAL-{horario.IdCalendarioRiego}-{claveMinutoActual}";
                bool ordenYaCreada = await db.ComandosRiego
                    .AnyAsync(c => c.ClaveIdempotencia == claveIdempotencia, ct);

                if (ordenYaCreada)
                    continue;

                int idInv = horario.IdInvernadero;
                var estadoRam = _ram.ObtenerOCrear(idInv);

                var planta = horario.Invernadero!.Planta!;
                decimal humedadActual = estadoRam.UltimaHumedad;
                decimal temperaturaActual = estadoRam.UltimaTemperatura;
                decimal humedadOptima = planta.HumedadOptima;

                string estadoComando = EstadosComandoRiego.Pendiente;
                string tipoEvento;
                string detalle;
                int duracionSeg;
                int prioridad;

                if (!estadoRam.SensorConectado)
                {
                    estadoComando = EstadosComandoRiego.Fallido;
                    tipoEvento = "SENSOR_SIN_DATOS";
                    duracionSeg = horario.TiempoRiegoSeg;
                    prioridad = 0;
                    detalle = $"[{planta.Nombre}] Horario {ahora:HH:mm} omitido: sensor sin datos recientes. No se creó una orden de riego para el ESP32.";
                }
                else if (humedadActual < planta.HumedadMinima ||
                         humedadActual > planta.HumedadMaxima ||
                         temperaturaActual < planta.TemperaturaMinima ||
                         temperaturaActual > planta.TemperaturaMaxima)
                {
                    estadoComando = EstadosComandoRiego.Omitido;
                    tipoEvento = "RIEGO_OMITIDO_PROTECCION";
                    duracionSeg = horario.TiempoRiegoSeg;
                    prioridad = 0;
                    detalle = $"[{planta.Nombre}] Horario {ahora:HH:mm} omitido: lectura fuera del rango permitido. Humedad {humedadActual}% y temperatura {temperaturaActual}°C.";
                }
                else if (humedadActual >= humedadOptima)
                {
                    estadoComando = EstadosComandoRiego.Omitido;
                    tipoEvento = "RIEGO_OMITIDO_RANGO_OPTIMO";
                    duracionSeg = horario.TiempoRiegoSeg;
                    prioridad = 0;
                    detalle = $"[{planta.Nombre}] Horario {ahora:HH:mm} omitido: humedad {humedadActual}% dentro del rango que no requiere riego ({humedadOptima}%–{planta.HumedadMaxima}%).";
                }
                else
                {
                    tipoEvento = "RIEGO_PROGRAMADO_PENDIENTE";
                    duracionSeg = horario.TiempoRiegoSeg;
                    prioridad = 50;
                    detalle = $"[{planta.Nombre}] Orden programada de {duracionSeg}s creada; humedad {humedadActual}%.";
                }

                horario.PospuestoHasta = null;

                db.ComandosRiego.Add(new ComandoRiego
                {
                    IdInvernadero = idInv,
                    IdCalendarioRiego = horario.IdCalendarioRiego,
                    Estado = estadoComando,
                    DuracionSeg = duracionSeg,
                    Prioridad = prioridad,
                    ClaveIdempotencia = claveIdempotencia,
                    FechaCreacion = ahora,
                    FechaExpiracion = estadoComando == EstadosComandoRiego.Pendiente
                        ? ahora.AddMinutes(5)
                        : ahora,
                    FechaFinalizacion = estadoComando == EstadosComandoRiego.Fallido
                        || estadoComando == EstadosComandoRiego.Omitido
                            ? ahora
                            : null,
                    Detalle = detalle
                });

                db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                {
                    IdInvernadero = idInv,
                    TipoEvento = tipoEvento,
                    Descripcion = detalle,
                    TiempoActivacionSeg = estadoComando == EstadosComandoRiego.Pendiente
                        ? duracionSeg
                        : 0,
                    HumedadMomento = humedadActual,
                    TemperaturaMomento = estadoRam.UltimaTemperatura,
                    FechaHora = ahora,
                    Leida = false
                });

                // Una fecha específica representa una sola ocurrencia.
                if (horario.FechaEspecifica.HasValue)
                    horario.State = false;

                huboCambiosBd = true;
            }

            if (huboCambiosBd)
            {
                await db.SaveChangesAsync(ct);
            }
        }

        /// <summary>
        /// Convierte un día de la semana a su nombre en español.
        /// </summary>
        /// <param name="dia">Día que se desea convertir.</param>
        /// <returns>El nombre del día en español.</returns>
        private static string ObtenerDiaSemanaEnEspanol(DayOfWeek dia) => dia switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miercoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sabado",
            DayOfWeek.Sunday => "Domingo",
            _ => string.Empty
        };
    }
}
