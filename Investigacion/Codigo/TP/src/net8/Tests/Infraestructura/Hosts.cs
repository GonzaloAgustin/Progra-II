using System.Net.Http;
using Inventario.Api;
using Inventario.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Pedidos.Api;
using Pedidos.Api.Data;
using Productos.Api;
using Productos.Api.Data;
using IProductosInventario = Inventario.Api.Clients.IProductosClient;
using IProductosPedidos = Pedidos.Api.Clients.IProductosClient;
using InventarioHttp = Pedidos.Api.Clients.IInventarioClient;
using InventarioRemoto = Pedidos.Api.Clients.InventarioClient;
using ProductosParaInventario = Inventario.Api.Clients.ProductosClient;
using ProductosParaPedidos = Pedidos.Api.Clients.ProductosClient;

namespace Tests.Infraestructura;

[CollectionDefinition("secuencial", DisableParallelization = true)]
public sealed class ColeccionSecuencial : ICollectionFixture<ColeccionSecuencial>
{
}

public sealed class Compuerta
{
    public bool SimularTimeout { get; set; }
    public bool SimularNoDisponible { get; set; }

    public void Restablecer()
    {
        SimularTimeout = false;
        SimularNoDisponible = false;
    }
}

public sealed class InterruptorHttp : DelegatingHandler
{
    private readonly Compuerta _compuerta;

    public InterruptorHttp(Compuerta compuerta, HttpMessageHandler inner) : base(inner)
    {
        _compuerta = compuerta;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_compuerta.SimularNoDisponible)
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));

        if (_compuerta.SimularTimeout)
        {
            var espera = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => espera.TrySetCanceled(cancellationToken));
            return espera.Task;
        }

        return base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        InnerHandler = null!;
        base.Dispose(disposing);
    }
}

public static class SqlLocal
{
    public static string Cadena(string baseDeDatos) =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={baseDeDatos};Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public static void Usar<TContext>(IServiceCollection services, string cadena) where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();
        services.AddDbContext<TContext>(options => options.UseSqlServer(cadena));
    }

    public static void Reemplazar<TServicio>(IServiceCollection services, TServicio instancia) where TServicio : class
    {
        services.RemoveAll<TServicio>();
        services.AddSingleton(instancia);
    }

    public static void Borrar<TContext>(IServiceProvider services) where TContext : DbContext
    {
        try
        {
            using var scope = services.CreateScope();
            scope.ServiceProvider.GetRequiredService<TContext>().Database.EnsureDeleted();
        }
        catch
        {
        }
    }
}

public sealed class ProductosApp : WebApplicationFactory<ProductosApiMarker>
{
    private readonly string _baseDeDatos = $"GcProductos_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            SqlLocal.Usar<ProductosDbContext>(services, SqlLocal.Cadena(_baseDeDatos));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            SqlLocal.Borrar<ProductosDbContext>(Services);
        base.Dispose(disposing);
    }
}

public sealed class InventarioApp : WebApplicationFactory<InventarioApiMarker>
{
    private readonly ProductosApp _productos;
    private readonly Compuerta _compuertaProductos;
    private readonly string _baseDeDatos = $"GcInventario_{Guid.NewGuid():N}";

    public InventarioApp(ProductosApp productos, Compuerta compuertaProductos)
    {
        _productos = productos;
        _compuertaProductos = compuertaProductos;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            SqlLocal.Usar<InventarioDbContext>(services, SqlLocal.Cadena(_baseDeDatos));
            var http = new HttpClient(new InterruptorHttp(_compuertaProductos, _productos.Server.CreateHandler()), disposeHandler: false)
            {
                BaseAddress = new Uri("http://localhost"),
                Timeout = TimeSpan.FromSeconds(2)
            };
            SqlLocal.Reemplazar<IProductosInventario>(services, new ProductosParaInventario(http));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            SqlLocal.Borrar<InventarioDbContext>(Services);
        base.Dispose(disposing);
    }
}

public sealed class PedidosApp : WebApplicationFactory<PedidosApiMarker>
{
    private readonly ProductosApp _productos;
    private readonly InventarioApp _inventario;
    private readonly Compuerta _compuertaProductos;
    private readonly Compuerta _compuertaInventario;
    private readonly string _baseDeDatos = $"GcPedidos_{Guid.NewGuid():N}";

    public PedidosApp(ProductosApp productos, InventarioApp inventario, Compuerta compuertaProductos, Compuerta compuertaInventario)
    {
        _productos = productos;
        _inventario = inventario;
        _compuertaProductos = compuertaProductos;
        _compuertaInventario = compuertaInventario;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reconciliacion:Habilitada"] = "false"
            });
        });
        builder.ConfigureServices(services =>
        {
            SqlLocal.Usar<PedidosDbContext>(services, SqlLocal.Cadena(_baseDeDatos));
            services.RemoveAll<IHostedService>();

            var productosHttp = new HttpClient(new InterruptorHttp(_compuertaProductos, _productos.Server.CreateHandler()), disposeHandler: false)
            {
                BaseAddress = new Uri("http://localhost"),
                Timeout = TimeSpan.FromSeconds(2)
            };
            var inventarioHttp = new HttpClient(new InterruptorHttp(_compuertaInventario, _inventario.Server.CreateHandler()), disposeHandler: false)
            {
                BaseAddress = new Uri("http://localhost"),
                Timeout = TimeSpan.FromSeconds(2)
            };
            SqlLocal.Reemplazar<IProductosPedidos>(services, new ProductosParaPedidos(productosHttp));
            SqlLocal.Reemplazar<InventarioHttp>(services, new InventarioRemoto(inventarioHttp));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            SqlLocal.Borrar<PedidosDbContext>(Services);
        base.Dispose(disposing);
    }
}

public sealed class Sistema : IDisposable
{
    public ProductosApp Productos { get; } = new();
    public Compuerta CompuertaProductos { get; } = new();
    public Compuerta CompuertaInventario { get; } = new();
    public InventarioApp Inventario { get; }
    public PedidosApp Pedidos { get; }

    public Sistema()
    {
        Inventario = new InventarioApp(Productos, CompuertaProductos);
        Pedidos = new PedidosApp(Productos, Inventario, CompuertaProductos, CompuertaInventario);
    }

    public void Dispose()
    {
        Pedidos.Dispose();
        Inventario.Dispose();
        Productos.Dispose();
        CompuertaProductos.Restablecer();
        CompuertaInventario.Restablecer();
    }
}
