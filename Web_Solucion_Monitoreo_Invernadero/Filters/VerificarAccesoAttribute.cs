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
            var session = context.HttpContext.Session;
            int? idUsuario = session.GetInt32("IdUsuario");

            // 1. Si no ha iniciado sesión, redirige obligatorio al Login
            if (!idUsuario.HasValue || idUsuario.Value <= 0)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            // 2. Si la ruta exige rol Administrador y el usuario no lo es, bloquea el paso
            if (SoloAdministrador)
            {
                string rol = session.GetString("RolUsuario") ?? "";
                if (!rol.Equals("Administrador", StringComparison.OrdinalIgnoreCase))
                {
                    context.Result = new RedirectToActionResult("Index", "Monitoreo", null);
                    return;
                }
            }

            base.OnActionExecuting(context);
        }
    }
}