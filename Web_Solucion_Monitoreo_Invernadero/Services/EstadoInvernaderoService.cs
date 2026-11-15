using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Web_Solucion_Monitoreo_Invernadero.Services
{
    public class PuntoGrafico
    {
        public string Hora { get; set; } = string.Empty;
        public decimal Humedad { get; set; }
        public decimal Temperatura { get; set; }
    }

    // Representa el estado individual en RAM de UN solo invernadero
    public class EstadoInvernadero
    {
        private readonly object _lock = new();

        public int IdInvernadero { get; set; }
        public decimal UltimaHumedad { get; private set; }
        public decimal UltimaTemperatura { get; private set; }
        public DateTime UltimaActualizacion { get; private set; } = DateTime.MinValue;
        public bool SensorConectado => (DateTime.Now - UltimaActualizacion).TotalSeconds <= 15;

        // Relojes independientes por invernadero
        public DateTime UltimoGuardadoHistorico { get; set; } = DateTime.MinValue;
        public DateTime UltimaAlertaCritica { get; set; } = DateTime.MinValue;

        // Caché de umbrales propios de la planta de este invernadero
        public int FrecuenciaRegistroMin { get; set; } = 60;
        public decimal TemperaturaMaxima { get; set; } = 24.00m;
        public decimal TemperaturaMinima { get; set; } = 18.00m;
        public decimal HumedadMaxima { get; set; } = 85.00m;
        public decimal HumedadMinima { get; set; } = 75.00m;
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

                while (_ultimosPuntos.Count > 20)
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
    }

    // Servicio Singleton global que administra todos los invernaderos en RAM
    public class EstadoInvernaderoService
    {
        // Diccionario seguro para hilos múltiples: Key = id_invernadero (1, 2, 3...)
        private readonly ConcurrentDictionary<int, EstadoInvernadero> _estados = new();

        // Obtiene el estado de un invernadero específico; si no existe aún en RAM, lo crea al instante
        public EstadoInvernadero ObtenerOCrear(int idInvernadero)
        {
            return _estados.GetOrAdd(idInvernadero, id => new EstadoInvernadero
            {
                IdInvernadero = id
            });
        }

        // Permite consultar todos los invernaderos activos a la vez (útil para el panel general)
        public IEnumerable<EstadoInvernadero> ObtenerTodos() => _estados.Values;
    }
}