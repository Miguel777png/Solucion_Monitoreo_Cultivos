using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Services
{

    /// Representa una muestra puntual de humedad y temperatura con marca de hora
    /// utilizada para alimentar los gráficos en tiempo real de la interfaz web.

    public class PuntoGrafico
    {
        public string Hora { get; set; } = string.Empty;
        public decimal Humedad { get; set; }
        public decimal Temperatura { get; set; }
    }

    /// Contenedor sincronizado (thread-safe) del estado operativo en memoria RAM de un invernadero.
    /// Almacena las últimas lecturas ambientales, el estado de conexión del sensor, una cola circular
    /// de hasta 40 puntos para Chart.js y los umbrales de la especie asignada, evaluando en tiempo
    /// real la activación de ventilación, calefacción y riego de emergencia continuo.

    public class EstadoInvernadero
    {
        private readonly object _lock = new();

        public int IdInvernadero { get; set; }
        public decimal UltimaHumedad { get; private set; }
        public decimal UltimaTemperatura { get; private set; }
        public DateTime UltimaActualizacion { get; private set; } = DateTime.MinValue;
        public bool SensorConectado => (DateTime.Now - UltimaActualizacion).TotalSeconds <= 25;

        public DateTime UltimoGuardadoHistorico { get; set; } = DateTime.MinValue;
        public DateTime UltimaAlertaUmbral { get; set; } = DateTime.MinValue;

        public int FrecuenciaRegistroMin { get; set; } = 60;
        public decimal TemperaturaMaxima { get; set; } = 24.00m;
        public decimal TemperaturaMinima { get; set; } = 18.00m;
        public decimal TemperaturaOptima { get; set; } = 21.00m;
        public decimal HumedadMaxima { get; set; } = 85.00m;
        public decimal HumedadMinima { get; set; } = 75.00m;
        public decimal HumedadOptima { get; set; } = 80.00m;
        public bool VentilacionActiva { get; private set; }
        public bool CalefaccionActiva { get; private set; }
        public bool RiegoEmergenciaActivo { get; private set; }
        public bool ConfiguracionCargada { get; set; } = false;

        private readonly Queue<PuntoGrafico> _ultimosPuntos = new();

        public void RegistrarLectura(decimal humedad, decimal temperatura)
        {
            lock (_lock)
            {
                UltimaHumedad = humedad;
                UltimaTemperatura = temperatura;
                UltimaActualizacion = DateTime.Now;

                _ultimosPuntos.Enqueue(new PuntoGrafico
                {
                    Hora = UltimaActualizacion.ToString("HH:mm:ss"),
                    Humedad = humedad,
                    Temperatura = temperatura
                });

                // Ampliamos a 40 puntos para que el desplazamiento continuo en Chart.js se note fluido
                while (_ultimosPuntos.Count > 40)
                {
                    _ultimosPuntos.Dequeue();
                }
            }
        }

        public List<PuntoGrafico> ObtenerPuntos()
        {
            lock (_lock)
            {
                return new List<PuntoGrafico>(_ultimosPuntos);
            }
        }

        public void ActualizarClimatizacion(decimal temperatura)
        {
            lock (_lock)
            {
                if (temperatura > TemperaturaMaxima)
                {
                    VentilacionActiva = true;
                    CalefaccionActiva = false;
                }
                else if (temperatura < TemperaturaMinima)
                {
                    CalefaccionActiva = true;
                    VentilacionActiva = false;
                }
                else
                {
                    VentilacionActiva = false;
                    CalefaccionActiva = false;
                }
            }
        }

        public bool ActualizarRiegoEmergencia(decimal humedad)
        {
            lock (_lock)
            {
                bool estadoAnterior = RiegoEmergenciaActivo;

                if (humedad < HumedadMinima)
                    RiegoEmergenciaActivo = true;
                else if (humedad >= HumedadMinima)
                    RiegoEmergenciaActivo = false;

                return estadoAnterior != RiegoEmergenciaActivo;
            }
        }
    }

    /// Servicio singleton que centraliza y administra en memoria RAM los estados en vivo de todos
    /// los invernaderos mediante un diccionario concurrente, desacoplando la telemetría de alta
    /// frecuencia de las escrituras periódicas en la base de datos.

    public class EstadoInvernaderoService
    {
        private readonly ConcurrentDictionary<int, EstadoInvernadero> _estados = new();

        public EstadoInvernadero ObtenerOCrear(int idInvernadero)
        {
            return _estados.GetOrAdd(idInvernadero, id => new EstadoInvernadero
            {
                IdInvernadero = id
            });
        }

        public IEnumerable<EstadoInvernadero> ObtenerTodos() => _estados.Values;
    }
}
