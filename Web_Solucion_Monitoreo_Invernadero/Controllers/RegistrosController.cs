using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    public class RegistrosController : Controller
    {
        private readonly ApplicationDbContext _db;

        public RegistrosController(ApplicationDbContext db)
        {
            _db = db;
        }

        // 1. REGISTRO HISTÓRICO AMBIENTAL CON FILTROS
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

            var consulta = _db.RegistroHistorico
                .Include(r => r.Invernadero)
                    .ThenInclude(i => i!.Planta)
                .Where(r => r.State);

            if (idInvernadero.HasValue && idInvernadero.Value > 0)
            {
                consulta = consulta.Where(r => r.IdInvernadero == idInvernadero.Value);
            }

            if (fechaDesde.HasValue)
            {
                consulta = consulta.Where(r => r.FechaHora >= fechaDesde.Value.Date);
            }

            if (fechaHasta.HasValue)
            {
                var limiteSuperior = fechaHasta.Value.Date.AddDays(1);
                consulta = consulta.Where(r => r.FechaHora < limiteSuperior);
            }

            var registros = await consulta
                .OrderByDescending(r => r.FechaHora)
                .Take(300)
                .ToListAsync();

            return View(registros);
        }

        // EXPORTACIÓN DIRECTA A CSV (EXCEL)
        public async Task<IActionResult> ExportarCsv(int? idInvernadero, DateTime? fechaDesde, DateTime? fechaHasta)
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

            var datos = await consulta.OrderByDescending(r => r.FechaHora).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("ID_Registro;Invernadero;Planta_Asignada;Humedad_Pct;Temperatura_C;Fecha_Hora");

            foreach (var r in datos)
            {
                sb.AppendLine($"{r.IdRegistro};{r.Invernadero?.Nombre};{r.Invernadero?.Planta?.Nombre};{r.Humedad};{r.Temperatura};{r.FechaHora:yyyy-MM-dd HH:mm:ss}");
            }

            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"Historial_Invernaderos_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        // 2. BITÁCORA COMPLETA DE RIEGOS Y ALERTAS AUTOMÁTICAS
        public async Task<IActionResult> Bitacora(int? idInvernadero)
        {
            ViewBag.Invernaderos = await _db.Invernaderos
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            ViewBag.IdInvernadero = idInvernadero;

            var consulta = _db.BitacoraRiegoAlertas
                .Include(b => b.Invernadero)
                .AsQueryable();

            if (idInvernadero.HasValue && idInvernadero.Value > 0)
            {
                consulta = consulta.Where(b => b.IdInvernadero == idInvernadero.Value);
            }

            var eventos = await consulta
                .OrderByDescending(b => b.FechaHora)
                .Take(200)
                .ToListAsync();

            return View(eventos);
        }

        // 3. AUDITORÍA DE CAMBIOS POR USUARIO
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