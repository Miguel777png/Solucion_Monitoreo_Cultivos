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
            // Si ya tiene sesión iniciada, entra directo al Monitor
            if (HttpContext.Session.GetInt32("IdUsuario").HasValue)
            {
                return RedirectToAction("Index", "Monitoreo");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarSesion(string correo, string password)
        {
            if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(password))
            {
                TempData["MensajeError"] = "Debe ingresar su correo y contraseña.";
                return RedirectToAction(nameof(Login));
            }

            string correoLimpio = correo.Trim().ToLower();

            var usuario = await _db.Usuarios
                .FirstOrDefaultAsync(u => u.Email.ToLower() == correoLimpio
                                       && u.PasswordHash == password
                                       && u.State);

            if (usuario == null)
            {
                TempData["MensajeError"] = "Correo o contraseña incorrectos, o usuario inactivo.";
                return RedirectToAction(nameof(Login));
            }

            // Mantiene exactamente las mismas variables de sesión de tu sistema y auditoría
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