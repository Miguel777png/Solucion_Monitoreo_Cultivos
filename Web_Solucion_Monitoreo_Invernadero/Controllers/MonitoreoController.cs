using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{

    /// Controlador principal del panel de supervisión en tiempo real (Monitor en Vivo).
    /// Carga los invernaderos activos junto con su especie y sensor vinculado, selecciona el sector
    /// a visualizar y obtiene las últimas alertas y eventos de riego para inicializar la interfaz.

    [VerificarAcceso]
    public class MonitoreoController : Controller
    {
        private readonly ApplicationDbContext _db;

        public MonitoreoController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int? idInvernadero)
        {
            var todosInvernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Include(i => i.Sensores)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            if (!todosInvernaderos.Any())
            {
                return RedirectToAction("Index", "Invernaderos");
            }

            var seleccionado = idInvernadero.HasValue
                ? todosInvernaderos.FirstOrDefault(i => i.IdInvernadero == idInvernadero.Value)
                : todosInvernaderos.First();

            seleccionado ??= todosInvernaderos.First();

            ViewBag.TodosInvernaderos = todosInvernaderos;
            ViewBag.UltimasAlertas = await _db.BitacoraRiegoAlertas
                .Where(b => b.IdInvernadero == seleccionado.IdInvernadero)
                .OrderByDescending(b => b.FechaHora)
                .Take(8)
                .ToListAsync();

            return View(seleccionado);
        }
    }
}