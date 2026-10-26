using System;
using System.Configuration;
using System.Net.Http;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using Inventario.Api.Clients;
using Inventario.Api.Data;
using Inventario.Api.Services;

namespace Inventario.Api;

public sealed class AplicacionApi : IDisposable
{
    public HttpConfiguration Configuracion { get; }
    public IContainer Contenedor { get; }

    public AplicacionApi(HttpConfiguration configuracion, IContainer contenedor)
    {
        Configuracion = configuracion;
        Contenedor = contenedor;
    }

    public void Dispose()
    {
        Contenedor.Dispose();
        Configuracion.Dispose();
    }
}

public static class Composicion
{
    public static AplicacionApi Crear(string cadena, IProductosCliente productos)
    {
        var config = new HttpConfiguration();
        WebApiConfig.Register(config);
        if (productos == null)
        {
            var baseUrl = ConfigurationManager.AppSettings["Servicios:Productos"] ?? "http://localhost:5011/";
            var segundos = 5;
            int.TryParse(ConfigurationManager.AppSettings["Servicios:TimeoutSegundos"], out segundos);
            if (segundos <= 0) segundos = 5;
            var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(segundos) };
            productos = new ProductosCliente(http);
        }

        var builder = new ContainerBuilder();
        builder.RegisterApiControllers(typeof(Composicion).Assembly);
        builder.Register(c => new InventarioContext(cadena)).InstancePerRequest();
        builder.RegisterInstance(productos).As<IProductosCliente>();
        builder.RegisterType<InventarioService>().As<IInventarioService>().InstancePerRequest();
        var contenedor = builder.Build();
        config.DependencyResolver = new AutofacWebApiDependencyResolver(contenedor);

        using (var db = new InventarioContext(cadena))
            db.Database.Initialize(false);

        return new AplicacionApi(config, contenedor);
    }
}
