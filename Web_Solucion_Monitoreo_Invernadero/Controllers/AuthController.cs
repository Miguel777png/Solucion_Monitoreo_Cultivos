using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AuthController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password)
        {
            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == password && u.State);

            if (usuario == null)
            {
                ViewBag.Error = "Correo o contraseña incorrectos.";
                return View();
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