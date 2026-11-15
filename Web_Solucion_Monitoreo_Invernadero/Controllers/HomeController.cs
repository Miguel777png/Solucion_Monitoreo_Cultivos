using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int idInvernadero = 1)
        {
            var invernadero = await _db.Invernaderos
                .Include(i => i.Planta)
                .Include(i => i.Sensores)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero && i.State);

            var ultimasAlertas = await _db.BitacoraRiegoAlertas
                .Where(b => b.IdInvernadero == idInvernadero)
                .OrderByDescending(b => b.FechaHora)
                .Take(5)
                .ToListAsync();

            ViewBag.Alertas = ultimasAlertas;
            ViewBag.ListaInvernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Where(i => i.State)
                .ToListAsync();

            return View(invernadero);
        }
    }
}