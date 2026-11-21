using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [VerificarAcceso]
    public class MonitoreoController : Controller
    {
        private readonly ApplicationDbContext _db;

        public MonitoreoController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int idInvernadero = 1)
        {
            var todosInvernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Include(i => i.Sensores)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            var seleccionado = todosInvernaderos.FirstOrDefault(i => i.IdInvernadero == idInvernadero)
                               ?? todosInvernaderos.FirstOrDefault();

            ViewBag.TodosInvernaderos = todosInvernaderos;

            ViewBag.UltimasAlertas = await _db.BitacoraRiegoAlertas
                .Where(b => b.IdInvernadero == (seleccionado != null ? seleccionado.IdInvernadero : 1))
                .OrderByDescending(b => b.FechaHora)
                .Take(8)
                .ToListAsync();

            return View(seleccionado);
        }
    }
}