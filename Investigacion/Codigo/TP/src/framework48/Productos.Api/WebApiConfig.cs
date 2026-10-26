using System.Web.Http;
using Newtonsoft.Json.Serialization;
using Productos.Api.Infrastructure;
using Swashbuckle.Application;

namespace Productos.Api;

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
            c.SingleApiVersion("v1", "Productos API")
                .Description("Catálogo de categorías y productos. Swagger 2.0 sobre ASP.NET Web API 2.");
            var xml = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Productos.Api.xml");
            if (System.IO.File.Exists(xml))
                c.IncludeXmlComments(xml);
        }).EnableSwaggerUi();
    }
}
