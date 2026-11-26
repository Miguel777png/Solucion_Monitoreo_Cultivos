using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Solucion_Monitoreo_Invernadero.Database;
using Web_Solucion_Monitoreo_Invernadero.Models;
using Web_Solucion_Monitoreo_Invernadero.Services;

namespace Web_Solucion_Monitoreo_Invernadero.Controllers
{

    /// <summary>
    /// Administra las cuentas de usuario. Solo puede acceder un administrador.
    /// </summary>
    [Authorize(Roles = "Administrador")]
    public class UsuariosController : Controller
    {
        private static readonly string[] RolesPermitidos = ["Administrador", "Operador"];

        private readonly ApplicationDbContext _db;
        private readonly AuditoriaService _auditoria;

        /// <summary>
        /// Inicializa el controlador de usuarios.
        /// </summary>
        /// <param name="db">Contexto de la base de datos.</param>
        /// <param name="auditoria">Servicio que registra los cambios.</param>
        public UsuariosController(ApplicationDbContext db, AuditoriaService auditoria)
        {
            _db = db;
            _auditoria = auditoria;
        }

        /// <summary>
        /// Muestra todos los usuarios registrados.
        /// </summary>
        /// <returns>La vista de gestión de usuarios.</returns>
        public async Task<IActionResult> Index()
        {
            ViewBag.IdUsuarioActual = ObtenerIdUsuarioActual();

            var usuarios = await _db.Usuarios
                .AsNoTracking()
                .OrderBy(u => u.IdUsuario)
                .ToListAsync();

            return View(usuarios);
        }

        /// <summary>
        /// Crea una nueva cuenta de usuario.
        /// </summary>
        /// <param name="nombre">Nombre del usuario.</param>
        /// <param name="email">Correo del usuario.</param>
        /// <param name="password">Contraseña de la cuenta.</param>
        /// <param name="rol">Rol asignado.</param>
        /// <returns>Una redirección al listado de usuarios.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(
            string nombre,
            string email,
            string password,
            string rol)
        {
            nombre = nombre?.Trim() ?? string.Empty;
            email = email?.Trim().ToLowerInvariant() ?? string.Empty;
            rol = NormalizarRol(rol);

            string? error = ValidarDatos(nombre, email, rol, password, passwordObligatoria: true);
            if (error != null)
            {
                TempData["MensajeError"] = error;
                return RedirectToAction(nameof(Index));
            }

            error = await ValidarUnicidadAsync(nombre, email);
            if (error != null)
            {
                TempData["MensajeError"] = error;
                return RedirectToAction(nameof(Index));
            }

            var usuario = new Usuario
            {
                Nombre = nombre,
                Email = email,
                PasswordHash = password,
                Rol = rol,
                State = true
            };

            _db.Usuarios.Add(usuario);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["MensajeError"] = "No fue posible crear el usuario. Verifica que el nombre y el correo no estén registrados.";
                return RedirectToAction(nameof(Index));
            }

            await _auditoria.RegistrarAsync(
                "CREAR_USUARIO",
                "Usuarios",
                $"Creó al usuario '{usuario.Nombre}' con rol {usuario.Rol} (ID #{usuario.IdUsuario}).");

            TempData["MensajeExito"] = $"Usuario '{usuario.Nombre}' creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Edita una cuenta distinta a la que tiene la sesión activa.
        /// </summary>
        /// <param name="idUsuario">Identificador del usuario.</param>
        /// <param name="nombre">Nuevo nombre.</param>
        /// <param name="email">Nuevo correo.</param>
        /// <param name="rol">Nuevo rol.</param>
        /// <param name="state">Indica si la cuenta queda activa.</param>
        /// <param name="nuevaPassword">Nueva contraseña opcional.</param>
        /// <returns>Una redirección al listado de usuarios.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int idUsuario,
            string nombre,
            string email,
            string rol,
            bool state,
            string? nuevaPassword)
        {
            int idActual = ObtenerIdUsuarioActual();
            if (idUsuario == idActual)
            {
                TempData["MensajeError"] = "No puedes editar tu propia cuenta mientras la sesión está activa.";
                return RedirectToAction(nameof(Index));
            }

            var usuario = await _db.Usuarios.FindAsync(idUsuario);
            if (usuario == null)
            {
                TempData["MensajeError"] = "El usuario solicitado no existe.";
                return RedirectToAction(nameof(Index));
            }

            nombre = nombre?.Trim() ?? string.Empty;
            email = email?.Trim().ToLowerInvariant() ?? string.Empty;
            rol = NormalizarRol(rol);
            nuevaPassword = string.IsNullOrWhiteSpace(nuevaPassword) ? null : nuevaPassword;

            string? error = ValidarDatos(nombre, email, rol, nuevaPassword, passwordObligatoria: false);
            if (error != null)
            {
                TempData["MensajeError"] = error;
                return RedirectToAction(nameof(Index));
            }

            error = await ValidarUnicidadAsync(nombre, email, idUsuario);
            if (error != null)
            {
                TempData["MensajeError"] = error;
                return RedirectToAction(nameof(Index));
            }

            usuario.Nombre = nombre;
            usuario.Email = email;
            usuario.Rol = rol;
            usuario.State = state;

            if (nuevaPassword != null)
            {
                usuario.PasswordHash = nuevaPassword;
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["MensajeError"] = "No fue posible actualizar el usuario. Verifica los datos únicos.";
                return RedirectToAction(nameof(Index));
            }

            await _auditoria.RegistrarAsync(
                "EDITAR_USUARIO",
                "Usuarios",
                $"Actualizó al usuario '{usuario.Nombre}' (ID #{usuario.IdUsuario}): rol {usuario.Rol}, estado {(usuario.State ? "Activo" : "Inactivo")}." );

            TempData["MensajeExito"] = $"Usuario '{usuario.Nombre}' actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Obtiene el identificador del usuario autenticado.
        /// </summary>
        /// <returns>El identificador guardado en los claims.</returns>
        private int ObtenerIdUsuarioActual()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out int idUsuario)
                ? idUsuario
                : throw new InvalidOperationException("No se encontró el usuario autenticado.");
        }

        /// <summary>
        /// Ajusta el nombre del rol a uno de los valores permitidos.
        /// </summary>
        /// <param name="rol">Rol recibido desde el formulario.</param>
        /// <returns>El rol normalizado o una cadena vacía.</returns>
        private static string NormalizarRol(string? rol)
        {
            return RolesPermitidos.FirstOrDefault(r => r.Equals(rol?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? string.Empty;
        }

        /// <summary>
        /// Valida los datos básicos de una cuenta.
        /// </summary>
        /// <param name="nombre">Nombre que se desea validar.</param>
        /// <param name="email">Correo que se desea validar.</param>
        /// <param name="rol">Rol que se desea validar.</param>
        /// <param name="password">Contraseña opcional.</param>
        /// <param name="passwordObligatoria">Indica si se exige una contraseña.</param>
        /// <returns>Un mensaje de error o <see langword="null"/> si los datos son válidos.</returns>
        private static string? ValidarDatos(
            string nombre,
            string email,
            string rol,
            string? password,
            bool passwordObligatoria)
        {
            if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
                return "El nombre es obligatorio y no puede superar 100 caracteres.";

            if (string.IsNullOrWhiteSpace(email) || email.Length > 120 || !new EmailAddressAttribute().IsValid(email))
                return "Ingresa un correo electrónico válido de hasta 120 caracteres.";

            if (!RolesPermitidos.Contains(rol))
                return "Selecciona un rol válido.";

            if (passwordObligatoria && string.IsNullOrWhiteSpace(password))
                return "La contraseña es obligatoria.";

            if (password != null && (password.Length < 6 || password.Length > 255))
                return "La contraseña debe tener entre 6 y 255 caracteres.";

            return null;
        }

        /// <summary>
        /// Revisa que el nombre y correo no estén ocupados.
        /// </summary>
        /// <param name="nombre">Nombre que se desea guardar.</param>
        /// <param name="email">Correo que se desea guardar.</param>
        /// <param name="idExcluir">Usuario que se excluye durante una edición.</param>
        /// <returns>Un mensaje de error o <see langword="null"/> si no hay duplicados.</returns>
        private async Task<string?> ValidarUnicidadAsync(string nombre, string email, int? idExcluir = null)
        {
            var consulta = _db.Usuarios.AsQueryable();
            if (idExcluir.HasValue)
            {
                consulta = consulta.Where(u => u.IdUsuario != idExcluir.Value);
            }

            if (await consulta.AnyAsync(u => u.Nombre == nombre))
                return "Ya existe un usuario con ese nombre.";

            if (await consulta.AnyAsync(u => u.Email == email))
                return "Ya existe un usuario con ese correo electrónico.";

            return null;
        }
    }
}
