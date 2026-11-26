using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{


    /// <summary>
    /// Controla el inicio y cierre de sesión de los usuarios.
    /// </summary>
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// Inicializa el controlador de autenticación.
        /// </summary>
        /// <param name="db">Contexto usado para consultar los usuarios.</param>
        public AuthController(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Muestra el formulario de inicio de sesión.
        /// </summary>
        /// <returns>La vista de acceso o una redirección al monitoreo.</returns>
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

        /// <summary>
        /// Valida las credenciales y crea la sesión del usuario.
        /// </summary>
        /// <param name="correo">Correo ingresado por el usuario.</param>
        /// <param name="password">Contraseña ingresada por el usuario.</param>
        /// <returns>Una redirección al monitoreo o nuevamente al login.</returns>
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

        /// <summary>
        /// Cierra la sesión y elimina la cookie de autenticación.
        /// </summary>
        /// <returns>Una redirección al inicio de sesión.</returns>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        /// <summary>
        /// Muestra la página cuando el usuario no tiene permiso.
        /// </summary>
        /// <returns>La vista de acceso denegado con estado 403.</returns>
        [HttpGet]
        [Authorize]
        public IActionResult AccesoDenegado()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}
