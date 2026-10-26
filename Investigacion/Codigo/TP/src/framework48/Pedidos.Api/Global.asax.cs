using System.Configuration;
using System.Web;
using System.Web.Http;

namespace Pedidos.Api;

public class WebApiApplication : HttpApplication
{
    protected void Application_Start()
    {
        var cadena = ConfigurationManager.ConnectionStrings["Pedidos"].ConnectionString;
        var aplicacion = Composicion.Crear(cadena, null, null);
        GlobalConfiguration.Configuration.DependencyResolver = aplicacion.Configuracion.DependencyResolver;
        WebApiConfig.Register(GlobalConfiguration.Configuration);
    }
}
