using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    public class InvernaderosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        public InvernaderosController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        public async Task<IActionResult> Index()
        {
            var invernaderos = await _db.Invernaderos
                .Include(i => i.Planta)
                .Include(i => i.Sensores)
                .Include(i => i.UsuarioModificacion)
                .Where(i => i.State)
                .OrderBy(i => i.IdInvernadero)
                .ToListAsync();

            // Traemos todas las plantas activas con su invernadero actual para filtrar disponibilidad 1:1 en la vista
            ViewBag.TodasPlantas = await _db.Plantas
                .Include(p => p.Invernadero)
                .Where(p => p.State)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            return View(invernaderos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarAsignacion(
            int idInvernadero,
            string nombre,
            string ubicacion,
            int idPlanta,
            int frecuenciaRegistroMin)
        {
            var inv = await _db.Invernaderos
                .Include(i => i.Planta)
                .FirstOrDefaultAsync(i => i.IdInvernadero == idInvernadero);

            if (inv == null) return RedirectToAction(nameof(Index));

            // Validar que la nueva planta no esté ocupada por otro invernadero (Relación estricta 1:1)
            bool ocupadaPorOtro = await _db.Invernaderos
                .AnyAsync(i => i.IdPlanta == idPlanta && i.IdInvernadero != idInvernadero && i.State);

            if (ocupadaPorOtro)
            {
                TempData["MensajeError"] = "Esa planta ya está asignada a otro invernadero (relación 1:1).";
                return RedirectToAction(nameof(Index));
            }

            var nuevaPlanta = await _db.Plantas.FindAsync(idPlanta);
            if (nuevaPlanta == null) return RedirectToAction(nameof(Index));

            string plantaAnterior = inv.Planta?.Nombre ?? "Sin planta";
            int idUsuario = _auditoria.ObtenerIdUsuarioActual();

            inv.Nombre = nombre.Trim();
            inv.Ubicacion = ubicacion.Trim();
            inv.IdPlanta = idPlanta;
            inv.FrecuenciaRegistroMin = frecuenciaRegistroMin;
            inv.IdUsuarioModificacion = idUsuario;
            inv.FechaModificacion = DateTime.Now;

            await _db.SaveChangesAsync();

            await _auditoria.RegistrarAsync(
                "ASIGNAR_PLANTA_INVERNADERO",
                inv.Nombre,
                $"Cambió planta de '{plantaAnterior}' a '{nuevaPlanta.Nombre}' y frecuencia histórica a {frecuenciaRegistroMin} min."
            );

            TempData["MensajeExito"] = $"{inv.Nombre} actualizado con '{nuevaPlanta.Nombre}' (Usuario ID #{idUsuario}).";
            return RedirectToAction(nameof(Index));
        }
    }
}