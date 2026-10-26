using System.Web;
using System.Web.Http;

namespace Productos.Api;

public class WebApiApplication : HttpApplication
{
    protected void Application_Start()
    {
        var cadena = System.Configuration.ConfigurationManager.ConnectionStrings["Productos"].ConnectionString;
        var aplicacion = Composicion.Crear(cadena);
        GlobalConfiguration.Configuration.DependencyResolver = aplicacion.Configuracion.DependencyResolver;
        WebApiConfig.Register(GlobalConfiguration.Configuration);
    }
}
