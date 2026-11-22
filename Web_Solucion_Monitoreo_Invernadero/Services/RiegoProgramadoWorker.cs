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

            var horariosActivos = await db.CalendarioRiego
                .Include(c => c.Invernadero)
                    .ThenInclude(i => i!.Planta)
                .Where(c => c.State && c.Invernadero != null && c.Invernadero.State && c.Invernadero.Planta != null)
                .ToListAsync(ct);

            bool huboCambiosBd = false;

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

                int idInv = horario.IdInvernadero;
                var estadoRam = _ram.ObtenerOCrear(idInv);

                if (estadoRam.UltimoMinutoEvaluadoCalendario == claveMinutoActual)
                    continue;

                estadoRam.UltimoMinutoEvaluadoCalendario = claveMinutoActual;

                var planta = horario.Invernadero!.Planta!;
                decimal humedadActual = estadoRam.UltimaHumedad;
                decimal humedadMinima = planta.HumedadMinima;
                decimal humedadOptima = (planta.HumedadMinima + planta.HumedadMaxima) / 2m;

                if (estadoRam.SensorConectado && humedadActual >= humedadOptima)
                {
                    // ZONA ÓPTIMA: Pospone el riego completo y ejecuta riego reducido de 20 segundos
                    horario.PospuestoHasta = DateOnly.FromDateTime(ahora.AddHours(2));
                    estadoRam.RiegoProgramadoPendiente = true;
                    estadoRam.SegundosRiegoProgramado = 20;

                    db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                    {
                        IdInvernadero = idInv,
                        TipoEvento = "RIEGO_REDUCIDO",
                        Descripcion = $"[{planta.Nombre}] Riego programado ({ahora:HH:mm}) pospuesto por humedad óptima ({humedadActual}% >= {humedadOptima:0.0}%): aplicado riego reducido de 20s.",
                        TiempoActivacionSeg = 20,
                        HumedadMomento = humedadActual,
                        TemperaturaMomento = estadoRam.UltimaTemperatura,
                        FechaHora = ahora,
                        Leida = false
                    });
                }
                else if (estadoRam.SensorConectado && humedadActual < humedadMinima)
                {
                    // BAJO EL MÍNIMO: Alerta crítica y riego de emergencia de 3 minutos (180 seg)
                    horario.PospuestoHasta = null;
                    estadoRam.RiegoProgramadoPendiente = true;
                    estadoRam.SegundosRiegoProgramado = 180;

                    db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                    {
                        IdInvernadero = idInv,
                        TipoEvento = "RIEGO_EMERGENCIA",
                        Descripcion = $"[{planta.Nombre}] Horario programado ({ahora:HH:mm}) coincidió con humedad crítica ({humedadActual}% < {humedadMinima}%): alerta y riego de 3 min (180s).",
                        TiempoActivacionSeg = 180,
                        HumedadMomento = humedadActual,
                        TemperaturaMomento = estadoRam.UltimaTemperatura,
                        FechaHora = ahora,
                        Leida = false
                    });
                }
                else
                {
                    // ENTRE MÍNIMO Y ÓPTIMO (o sin lectura activa): Ejecuta el riego programado normal
                    horario.PospuestoHasta = null;
                    estadoRam.RiegoProgramadoPendiente = true;
                    estadoRam.SegundosRiegoProgramado = horario.TiempoRiegoSeg;

                    db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                    {
                        IdInvernadero = idInv,
                        TipoEvento = "RIEGO_PROGRAMADO",
                        Descripcion = $"[{planta.Nombre}] Riego programado ejecutado ({ahora:HH:mm}) durante {horario.TiempoRiegoSeg}s (humedad {humedadActual}% bajo óptimo {humedadOptima:0.0}%).",
                        TiempoActivacionSeg = horario.TiempoRiegoSeg,
                        HumedadMomento = humedadActual,
                        TemperaturaMomento = estadoRam.UltimaTemperatura,
                        FechaHora = ahora,
                        Leida = false
                    });
                }

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