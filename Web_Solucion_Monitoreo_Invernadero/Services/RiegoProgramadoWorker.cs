using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;

namespace Web_Solucion_Monitoreo_Invernadero.Services
{
    public class RiegoProgramadoWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly EstadoInvernaderoService _ram;

        public RiegoProgramadoWorker(IServiceScopeFactory scopeFactory, EstadoInvernaderoService ram)
        {
            _scopeFactory = scopeFactory;
            _ram = ram;
        }

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
                decimal humedadMinima = planta.HumedadMinima;
                decimal humedadOptima = (planta.HumedadMinima + planta.HumedadMaxima) / 2m;

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
                else if (humedadActual >= humedadOptima)
                {
                    tipoEvento = "RIEGO_REDUCIDO_PENDIENTE";
                    duracionSeg = 20;
                    prioridad = 25;
                    detalle = $"[{planta.Nombre}] Orden reducida de 20s creada; humedad óptima ({humedadActual}%).";
                }
                else if (humedadActual < humedadMinima)
                {
                    tipoEvento = "RIEGO_EMERGENCIA_PENDIENTE";
                    duracionSeg = 180;
                    prioridad = 100;
                    detalle = $"[{planta.Nombre}] Orden de emergencia de 180s creada; humedad {humedadActual}% < {humedadMinima}%.";
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
