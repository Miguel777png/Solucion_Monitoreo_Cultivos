using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Filters;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    [VerificarAcceso]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AuthController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            // Si ya tiene sesión iniciada, entra directo al Monitor
            if (HttpContext.Session.GetInt32("IdUsuario").HasValue)
            {
                return RedirectToAction("Index", "Monitoreo");
            }

            var usuarios = await _db.Usuarios
                .Where(u => u.State)
                .OrderBy(u => u.IdUsuario)
                .ToListAsync();

            return View(usuarios);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarSesion(int idUsuario)
        {
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.IdUsuario == idUsuario && u.State);

            if (usuario == null)
            {
                TempData["MensajeError"] = "Usuario seleccionado no válido o inactivo.";
                return RedirectToAction(nameof(Login));
            }

            HttpContext.Session.SetInt32("IdUsuario", usuario.IdUsuario);
            HttpContext.Session.SetString("NombreUsuario", usuario.Nombre);
            HttpContext.Session.SetString("RolUsuario", usuario.Rol);

            return RedirectToAction("Index", "Monitoreo");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }
    }
}