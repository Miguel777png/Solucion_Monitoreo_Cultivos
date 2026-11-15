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
            // Bucle infinito mientras el servidor esté encendido
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

                // Revisa cada 20 segundos para no perder ningún minuto exacto
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }

        private async Task EvaluarCalendariosAsync(CancellationToken ct)
        {
            DateTime ahora = DateTime.Now;
            string claveMinutoActual = ahora.ToString("yyyy-MM-dd HH:mm");
            string diaSemanaEs = ObtenerDiaSemanaEnEspanol(ahora.DayOfWeek);

            // Como DbContext es Scoped y el Worker es Singleton, creamos un Scope temporal por ciclo
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var horariosActivos = await db.CalendarioRiego
                .Include(c => c.Planta)
                    .ThenInclude(p => p!.Invernadero)
                .Where(c => c.State && c.Planta != null && c.Planta.Invernadero != null && c.Planta.Invernadero.State)
                .ToListAsync(ct);

            bool huboCambiosBd = false;

            foreach (var horario in horariosActivos)
            {
                // 1. Verificar si coincide la hora y el minuto exacto (TimeOnly en .NET 8)
                if (horario.HoraRiego.Hour != ahora.Hour || horario.HoraRiego.Minute != ahora.Minute)
                    continue;

                // 2. Verificar si coincide la fecha específica O el día de la semana recurrente
                bool coincideFecha = horario.FechaEspecifica.HasValue &&
                                     horario.FechaEspecifica.Value == DateOnly.FromDateTime(ahora);

                bool coincideDia = !horario.FechaEspecifica.HasValue &&
                                   !string.IsNullOrWhiteSpace(horario.DiaSemana) &&
                                   horario.DiaSemana.Trim().Equals(diaSemanaEs, StringComparison.OrdinalIgnoreCase);

                if (!coincideFecha && !coincideDia)
                    continue;

                int idInv = horario.Planta!.Invernadero!.IdInvernadero;
                var estadoRam = _ram.ObtenerOCrear(idInv);

                // Evita ejecutar dos veces el mismo horario dentro del mismo minuto
                if (estadoRam.UltimoMinutoEvaluadoCalendario == claveMinutoActual)
                    continue;

                estadoRam.UltimoMinutoEvaluadoCalendario = claveMinutoActual;

                // 3. Lógica inteligente: si el sensor está en vivo y la humedad ya es óptima, posponemos el riego
                decimal humedadActual = estadoRam.UltimaHumedad;
                decimal humedadMinima = horario.Planta.HumedadMinima;

                if (estadoRam.SensorConectado && humedadActual >= humedadMinima)
                {
                    horario.PospuestoHasta = DateOnly.FromDateTime(ahora.AddHours(2));

                    db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                    {
                        IdInvernadero = idInv,
                        TipoEvento = "RIEGO_POSPUESTO",
                        Descripcion = $"Riego programado ({ahora:HH:mm}) pospuesto: humedad actual ({humedadActual}%) ya es óptima (>= {humedadMinima}%).",
                        TiempoActivacionSeg = 0,
                        HumedadMomento = humedadActual,
                        TemperaturaMomento = estadoRam.UltimaTemperatura,
                        FechaHora = ahora,
                        Leida = false
                    });
                }
                else
                {
                    // Ordenamos encender la regadera en el próximo pulso del ESP32
                    estadoRam.RiegoProgramadoPendiente = true;
                    estadoRam.SegundosRiegoProgramado = horario.TiempoRiegoSeg;
                    horario.PospuestoHasta = null;

                    db.BitacoraRiegoAlertas.Add(new BitacoraRiegoAlerta
                    {
                        IdInvernadero = idInv,
                        TipoEvento = "RIEGO_PROGRAMADO",
                        Descripcion = $"Riego programado ejecutado ({ahora:HH:mm}) durante {horario.TiempoRiegoSeg}s.",
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