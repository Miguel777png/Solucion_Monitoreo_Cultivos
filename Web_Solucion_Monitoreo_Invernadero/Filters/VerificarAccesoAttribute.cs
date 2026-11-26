using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Web_Solucion_Monitoreo_Invernadero.Filters
{
    /// <summary>
    /// Verifica que exista una sesión válida antes de ejecutar una acción.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class VerificarAccesoAttribute : ActionFilterAttribute
    {
        /// <summary>
        /// Indica si la acción se limita a administradores.
        /// </summary>
        public bool SoloAdministrador { get; set; } = false;

        /// <summary>
        /// Revisa la autenticación y el rol antes de continuar.
        /// </summary>
        /// <param name="context">Contexto de la acción solicitada.</param>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var usuario = context.HttpContext.User;

            // La cookie autenticada y sus claims son la fuente de autorización.
            if (usuario.Identity?.IsAuthenticated != true)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            if (SoloAdministrador && !usuario.IsInRole("Administrador"))
            {
                context.Result = new RedirectToActionResult("AccesoDenegado", "Auth", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
