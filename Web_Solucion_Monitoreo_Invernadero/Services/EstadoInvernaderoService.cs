using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Services
{

    /// <summary>
    /// Representa un punto que se muestra en el gráfico del monitoreo.
    /// </summary>
    public class PuntoGrafico
    {
        /// <summary>Hora de la lectura.</summary>
        public string Hora { get; set; } = string.Empty;
        /// <summary>Humedad de la lectura.</summary>
        public decimal Humedad { get; set; }
        /// <summary>Temperatura de la lectura.</summary>
        public decimal Temperatura { get; set; }
    }

    /// <summary>
    /// Mantiene en memoria el estado actual de un invernadero.
    /// </summary>
    public class EstadoInvernadero
    {
        private readonly object _lock = new();

        /// <summary>Identificador del invernadero.</summary>
        public int IdInvernadero { get; set; }
        /// <summary>Última humedad recibida.</summary>
        public decimal UltimaHumedad { get; private set; }
        /// <summary>Última temperatura recibida.</summary>
        public decimal UltimaTemperatura { get; private set; }
        /// <summary>Fecha de la última lectura.</summary>
        public DateTime UltimaActualizacion { get; private set; } = DateTime.MinValue;
        /// <summary>Indica si el sensor envió datos recientemente.</summary>
        public bool SensorConectado => (DateTime.Now - UltimaActualizacion).TotalSeconds <= 25;

        /// <summary>Fecha del último guardado histórico.</summary>
        public DateTime UltimoGuardadoHistorico { get; set; } = DateTime.MinValue;
        /// <summary>Fecha de la última alerta de umbral.</summary>
        public DateTime UltimaAlertaUmbral { get; set; } = DateTime.MinValue;

        /// <summary>Frecuencia de guardado en minutos.</summary>
        public int FrecuenciaRegistroMin { get; set; } = 60;
        /// <summary>Temperatura máxima permitida.</summary>
        public decimal TemperaturaMaxima { get; set; } = 24.00m;
        /// <summary>Temperatura mínima permitida.</summary>
        public decimal TemperaturaMinima { get; set; } = 18.00m;
        /// <summary>Inicio del rango óptimo de temperatura.</summary>
        public decimal TemperaturaOptima { get; set; } = 21.00m;
        /// <summary>Humedad máxima permitida.</summary>
        public decimal HumedadMaxima { get; set; } = 85.00m;
        /// <summary>Humedad mínima permitida.</summary>
        public decimal HumedadMinima { get; set; } = 75.00m;
        /// <summary>Inicio del rango óptimo de humedad.</summary>
        public decimal HumedadOptima { get; set; } = 80.00m;
        /// <summary>Indica si la ventilación está activa.</summary>
        public bool VentilacionActiva { get; private set; }
        /// <summary>Indica si la calefacción está activa.</summary>
        public bool CalefaccionActiva { get; private set; }
        /// <summary>Indica si el riego de emergencia está activo.</summary>
        public bool RiegoEmergenciaActivo { get; private set; }
        /// <summary>Indica si ya se cargaron los rangos de la planta.</summary>
        public bool ConfiguracionCargada { get; set; } = false;

        private readonly Queue<PuntoGrafico> _ultimosPuntos = new();

        /// <summary>
        /// Registra una lectura nueva y la agrega al gráfico.
        /// </summary>
        /// <param name="humedad">Humedad recibida desde el sensor.</param>
        /// <param name="temperatura">Temperatura recibida desde el sensor.</param>
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

        /// <summary>
        /// Obtiene una copia de los puntos recientes del gráfico.
        /// </summary>
        /// <returns>La lista de lecturas recientes.</returns>
        public List<PuntoGrafico> ObtenerPuntos()
        {
            lock (_lock)
            {
                return new List<PuntoGrafico>(_ultimosPuntos);
            }
        }

        /// <summary>
        /// Decide si se activa la ventilación o calefacción.
        /// </summary>
        /// <param name="temperatura">Temperatura actual del invernadero.</param>
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

        /// <summary>
        /// Activa o detiene el riego de emergencia según la humedad.
        /// </summary>
        /// <param name="humedad">Humedad actual del invernadero.</param>
        /// <returns><see langword="true"/> cuando el estado del riego cambió.</returns>
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

    /// <summary>
    /// Administra los estados en memoria de todos los invernaderos.
    /// </summary>
    public class EstadoInvernaderoService
    {
        private readonly ConcurrentDictionary<int, EstadoInvernadero> _estados = new();

        /// <summary>
        /// Obtiene el estado de un invernadero o crea uno nuevo.
        /// </summary>
        /// <param name="idInvernadero">Identificador del invernadero.</param>
        /// <returns>El estado guardado en memoria.</returns>
        public EstadoInvernadero ObtenerOCrear(int idInvernadero)
        {
            return _estados.GetOrAdd(idInvernadero, id => new EstadoInvernadero
            {
                IdInvernadero = id
            });
        }

        /// <summary>
        /// Obtiene todos los estados creados en memoria.
        /// </summary>
        /// <returns>La colección de estados disponibles.</returns>
        public IEnumerable<EstadoInvernadero> ObtenerTodos() => _estados.Values;
    }
}
