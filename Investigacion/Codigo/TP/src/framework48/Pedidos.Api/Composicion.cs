using System;
using System.Configuration;
using System.Net.Http;
using System.Web.Http;
using Autofac;
using Autofac.Integration.WebApi;
using Pedidos.Api.Clients;
using Pedidos.Api.Data;
using Pedidos.Api.Services;

namespace Pedidos.Api;

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
    public static AplicacionApi Crear(string cadena, IProductosCliente productos, IInventarioCliente inventario)
    {
        var config = new HttpConfiguration();
        WebApiConfig.Register(config);
        var segundos = 5;
        int.TryParse(ConfigurationManager.AppSettings["Servicios:TimeoutSegundos"], out segundos);
        if (segundos <= 0) segundos = 5;

        if (productos == null)
        {
            var http = new HttpClient
            {
                BaseAddress = new Uri(ConfigurationManager.AppSettings["Servicios:Productos"] ?? "http://localhost:5011/"),
                Timeout = TimeSpan.FromSeconds(segundos)
            };
            productos = new ProductosCliente(http);
        }

        if (inventario == null)
        {
            var http = new HttpClient
            {
                BaseAddress = new Uri(ConfigurationManager.AppSettings["Servicios:Inventario"] ?? "http://localhost:5012/"),
                Timeout = TimeSpan.FromSeconds(segundos)
            };
            inventario = new InventarioCliente(http);
        }

        var builder = new ContainerBuilder();
        builder.RegisterApiControllers(typeof(Composicion).Assembly);
        builder.Register(c => new PedidosContext(cadena)).InstancePerRequest();
        builder.RegisterInstance(productos).As<IProductosCliente>();
        builder.RegisterInstance(inventario).As<IInventarioCliente>();
        builder.RegisterType<PedidoService>().As<IPedidoService>().InstancePerRequest();
        var contenedor = builder.Build();
        config.DependencyResolver = new AutofacWebApiDependencyResolver(contenedor);
        using (var db = new PedidosContext(cadena))
            db.Database.Initialize(false);
        return new AplicacionApi(config, contenedor);
    }
}
