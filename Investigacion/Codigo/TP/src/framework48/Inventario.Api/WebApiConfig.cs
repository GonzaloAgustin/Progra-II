using System.Web.Http;
using Inventario.Api.Infrastructure;
using Newtonsoft.Json.Serialization;
using Swashbuckle.Application;

namespace Inventario.Api;

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
            c.SingleApiVersion("v1", "Inventario API")
                .Description("Stock, reservas atómicas y liberaciones. Swagger 2.0.");
        }).EnableSwaggerUi();
    }
}
