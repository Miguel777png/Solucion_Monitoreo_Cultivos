using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Web_Solucion_Monitoreo_Invernadero.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class VerificarAccesoAttribute : ActionFilterAttribute
    {
        public bool SoloAdministrador { get; set; } = false;

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
