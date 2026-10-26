using System.Configuration;
using System.Web;
using System.Web.Http;

namespace Inventario.Api;

public class WebApiApplication : HttpApplication
{
    protected void Application_Start()
    {
        var cadena = ConfigurationManager.ConnectionStrings["Inventario"].ConnectionString;
        var aplicacion = Composicion.Crear(cadena, null);
        GlobalConfiguration.Configuration.DependencyResolver = aplicacion.Configuracion.DependencyResolver;
        WebApiConfig.Register(GlobalConfiguration.Configuration);
    }
}
