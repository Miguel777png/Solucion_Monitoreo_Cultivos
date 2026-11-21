using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [VerificarAcceso] // Protege TODAS las acciones de este controlador (exige sesión iniciada)
    public class RegistrosController : Controller
    {
        private readonly ApplicationDbContext _db;

        public RegistrosController(ApplicationDbContext db)
        {
            _db = db;
        }

        // 1. REGISTRO HISTÓRICO AMBIENTAL CON FILTROS (Acceso: Admin y Jardinero)
        public async Task<IActionResult> Historial(int? idInvernadero, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            ViewBag.Invernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            ViewBag.IdInvernadero = idInvernadero;
            ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
            ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");

            var registros = await ConsultarHistorialAsync(idInvernadero, fechaDesde, fechaHasta, 300);
            return View(registros);
        }

        // ENDPOINT JSON EN VIVO PARA LA TABLA DE HISTORIAL (Acceso: Admin y Jardinero)
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> HistorialJson(int? idInvernadero, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var datos = await ConsultarHistorialAsync(idInvernadero, fechaDesde, fechaHasta, 300);
            var resultado = datos.Select(r => new
            {
                idRegistro = r.IdRegistro,
                invernadero = r.Invernadero?.Nombre ?? "",
                planta = r.Invernadero?.Planta?.Nombre ?? "",
                humedad = r.Humedad.ToString("0.00"),
                temperatura = r.Temperatura.ToString("0.00"),
                fechaHora = r.FechaHora.ToString("dd/MM/yyyy HH:mm:ss")
            });

            return Json(resultado);
        }

        private async Task<List<Models.RegistroHistorico>> ConsultarHistorialAsync(
            int? idInvernadero, DateTime? fechaDesde, DateTime? fechaHasta, int limite)
        {
            var consulta = _db.RegistroHistorico
                .Include(r => r.Invernadero)
                    .ThenInclude(i => i!.Planta)
                .Where(r => r.State);

            if (idInvernadero.HasValue && idInvernadero.Value > 0)
                consulta = consulta.Where(r => r.IdInvernadero == idInvernadero.Value);

            if (fechaDesde.HasValue)
                consulta = consulta.Where(r => r.FechaHora >= fechaDesde.Value.Date);

            if (fechaHasta.HasValue)
                consulta = consulta.Where(r => r.FechaHora < fechaHasta.Value.Date.AddDays(1));

            return await consulta
                .OrderByDescending(r => r.FechaHora)
                .Take(limite)
                .ToListAsync();
        }

        // EXPORTACIÓN DIRECTA A CSV (EXCEL) (Acceso: Admin y Jardinero)
        public async Task<IActionResult> ExportarCsv(int? idInvernadero, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var datos = await ConsultarHistorialAsync(idInvernadero, fechaDesde, fechaHasta, 5000);

            var sb = new StringBuilder();
            sb.AppendLine("ID_Registro;Invernadero;Planta_Asignada;Humedad_Pct;Temperatura_C;Fecha_Hora");

            foreach (var r in datos)
            {
                sb.AppendLine($"{r.IdRegistro};{r.Invernadero?.Nombre};{r.Invernadero?.Planta?.Nombre};{r.Humedad};{r.Temperatura};{r.FechaHora:yyyy-MM-dd HH:mm:ss}");
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"Historial_Invernaderos_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // 2. BITÁCORA COMPLETA DE RIEGOS Y ALERTAS CON FILTROS COMBINADOS (Acceso: Admin y Jardinero)
        public async Task<IActionResult> Bitacora(
            int? idInvernadero, string? tipoEvento, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            ViewBag.Invernaderos = await _db.Invernaderos
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            ViewBag.IdInvernadero = idInvernadero;
            ViewBag.TipoEvento = tipoEvento;
            ViewBag.FechaDesde = fechaDesde?.ToString("yyyy-MM-dd");
            ViewBag.FechaHasta = fechaHasta?.ToString("yyyy-MM-dd");

            var eventos = await ConsultarBitacoraAsync(idInvernadero, tipoEvento, fechaDesde, fechaHasta, 200);
            return View(eventos);
        }

        // ENDPOINT JSON EN VIVO PARA LA TABLA DE BITÁCORA (Acceso: Admin y Jardinero)
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> BitacoraJson(
            int? idInvernadero, string? tipoEvento, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var eventos = await ConsultarBitacoraAsync(idInvernadero, tipoEvento, fechaDesde, fechaHasta, 200);
            var resultado = eventos.Select(b => new
            {
                fechaHora = b.FechaHora.ToString("dd/MM/yyyy HH:mm:ss"),
                invernadero = b.Invernadero?.Nombre ?? "",
                tipoEvento = b.TipoEvento,
                descripcion = b.Descripcion,
                humedad = b.HumedadMomento.ToString("0.00"),
                temperatura = b.TemperaturaMomento.ToString("0.00"),
                tiempoSeg = b.TiempoActivacionSeg
            });

            return Json(resultado);
        }

        private async Task<List<Models.BitacoraRiegoAlerta>> ConsultarBitacoraAsync(
            int? idInvernadero, string? tipoEvento, DateTime? fechaDesde, DateTime? fechaHasta, int limite)
        {
            var consulta = _db.BitacoraRiegoAlertas
                .Include(b => b.Invernadero)
                .AsQueryable();

            if (idInvernadero.HasValue && idInvernadero.Value > 0)
                consulta = consulta.Where(b => b.IdInvernadero == idInvernadero.Value);

            if (!string.IsNullOrWhiteSpace(tipoEvento))
                consulta = consulta.Where(b => b.TipoEvento == tipoEvento);

            if (fechaDesde.HasValue)
                consulta = consulta.Where(b => b.FechaHora >= fechaDesde.Value.Date);

            if (fechaHasta.HasValue)
                consulta = consulta.Where(b => b.FechaHora < fechaHasta.Value.Date.AddDays(1));

            return await consulta
                .OrderByDescending(b => b.FechaHora)
                .Take(limite)
                .ToListAsync();
        }

        // 3. AUDITORÍA DE CAMBIOS POR USUARIO (Acceso EXCLUSIVO: Administrador)
        [VerificarAcceso(SoloAdministrador = true)]
        public async Task<IActionResult> Auditoria(int? idUsuario)
        {
            ViewBag.Usuarios = await _db.Usuarios
                .Where(u => u.State)
                .OrderBy(u => u.IdUsuario)
                .ToListAsync();

            ViewBag.IdUsuario = idUsuario;

            var consulta = _db.AuditoriaCambios
                .Include(a => a.Usuario)
                .AsQueryable();

            if (idUsuario.HasValue && idUsuario.Value > 0)
            {
                consulta = consulta.Where(a => a.IdUsuario == idUsuario.Value);
            }

            var auditorias = await consulta
                .OrderByDescending(a => a.FechaHora)
                .Take(200)
                .ToListAsync();

            return View(auditorias);
        }
    }
}