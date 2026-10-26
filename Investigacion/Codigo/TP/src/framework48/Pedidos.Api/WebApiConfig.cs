using System.Web.Http;
using Newtonsoft.Json.Serialization;
using Pedidos.Api.Infrastructure;
using Swashbuckle.Application;

namespace Pedidos.Api;

public static class WebApiConfig
{
    public static void Register(HttpConfiguration config)
    {
        config.MapHttpAttributeRoutes();
        config.Filters.Add(new ErrorFilter());
        config.Filters.Add(new ValidacionFilter());
        config.MessageHandlers.Add(new CorrelationHandler());
        var json = config.Formatters.JsonFormatter;
        json.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        json.SerializerSettings.DateTimeZoneHandling = Newtonsoft.Json.DateTimeZoneHandling.Utc;
        config.Formatters.Remove(config.Formatters.XmlFormatter);
        config.EnableSwagger(c =>
        {
            c.SingleApiVersion("v1", "Pedidos API")
                .Description("Pedidos, cancelación y reconciliación. El precio lo calcula el servidor. Swagger 2.0.");
        }).EnableSwaggerUi();
    }
}
