using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{

    /// <summary>
    /// Prepara la información que se muestra en el monitoreo en vivo.
    /// </summary>
    [VerificarAcceso]
    public class MonitoreoController : Controller
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// Inicializa el controlador de monitoreo.
        /// </summary>
        /// <param name="db">Contexto de la base de datos.</param>
        public MonitoreoController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Muestra el invernadero seleccionado junto con sus últimas alertas.
        /// </summary>
        /// <param name="idInvernadero">Invernadero que se desea visualizar.</param>
        /// <returns>La vista de monitoreo o una redirección si no existen invernaderos.</returns>
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
