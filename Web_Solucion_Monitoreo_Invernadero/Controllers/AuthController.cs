using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{


    /// 
    /// Controlador encargado de la autenticación y gestión de sesiones de usuario.
    /// Valida credenciales contra la base de datos, emite cookies de autenticación basadas en Claims
    /// junto con variables de sesión HTTP, y gestiona el cierre de sesión y vistas de acceso denegado.
    /// 

    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AuthController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true || HttpContext.Session.GetInt32("IdUsuario").HasValue)
            {
                return RedirectToAction("Index", "Monitoreo");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IniciarSesion(string correo, string password)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Monitoreo");
            }

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

            HttpContext.Session.SetInt32("IdUsuario", usuario.IdUsuario);
            HttpContext.Session.SetString("NombreUsuario", usuario.Nombre);
            HttpContext.Session.SetString("RolUsuario", usuario.Rol);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuario.IdUsuario.ToString()),
                new(ClaimTypes.Name, usuario.Nombre),
                new(ClaimTypes.Email, usuario.Email),
                new(ClaimTypes.Role, usuario.Rol)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(4)
                });

            return RedirectToAction("Index", "Monitoreo");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [Authorize]
        public IActionResult AccesoDenegado()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}
