using System;
using System.Configuration;
using Autofac;
using Autofac.Integration.WebApi;
using Productos.Api.Data;
using Productos.Api.Services;
using System.Web.Http;

namespace Productos.Api;

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
    public static AplicacionApi Crear(string cadena)
    {
        var config = new HttpConfiguration();
        WebApiConfig.Register(config);

        var builder = new ContainerBuilder();
        builder.RegisterApiControllers(typeof(Composicion).Assembly);
        builder.Register(c => new ProductosContext(cadena)).InstancePerRequest();
        builder.RegisterType<CategoriaService>().As<ICategoriaService>().InstancePerRequest();
        builder.RegisterType<ProductoService>().As<IProductoService>().InstancePerRequest();
        var contenedor = builder.Build();
        config.DependencyResolver = new AutofacWebApiDependencyResolver(contenedor);

        using (var db = new ProductosContext(cadena))
        {
            db.Database.Initialize(force: false);
            if (string.Equals(ConfigurationManager.AppSettings["SeedDemoData"], "true", StringComparison.OrdinalIgnoreCase))
                DemoSeeder.Sembrar(db);
        }

        return new AplicacionApi(config, contenedor);
    }
}
