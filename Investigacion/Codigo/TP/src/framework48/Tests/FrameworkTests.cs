using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tests;

[CollectionDefinition("fw", DisableParallelization = true)]
public sealed class ColeccionFw : ICollectionFixture<ColeccionFw>
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

public sealed class Interruptor : DelegatingHandler
{
    private readonly Compuerta _compuerta;
    public Interruptor(Compuerta compuerta, HttpMessageHandler inner) : base(inner) { _compuerta = compuerta; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_compuerta.SimularNoDisponible)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        if (_compuerta.SimularTimeout)
        {
            var espera = new TaskCompletionSource<HttpResponseMessage>();
            cancellationToken.Register(() => espera.TrySetCanceled(cancellationToken));
            return espera.Task;
        }
        return base.SendAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        InnerHandler = null;
        base.Dispose(disposing);
    }
}

public sealed class Sistema : IDisposable
{
    private readonly string _productosDb = "Server=(localdb)\\MSSQLLocalDB;Database=FwP_" + Guid.NewGuid().ToString("N") + ";Integrated Security=True;MultipleActiveResultSets=True";
    private readonly string _inventarioDb = "Server=(localdb)\\MSSQLLocalDB;Database=FwI_" + Guid.NewGuid().ToString("N") + ";Integrated Security=True;MultipleActiveResultSets=True";
    private readonly string _pedidosDb = "Server=(localdb)\\MSSQLLocalDB;Database=FwD_" + Guid.NewGuid().ToString("N") + ";Integrated Security=True;MultipleActiveResultSets=True";
    private readonly Productos.Api.AplicacionApi _productosApp;
    private readonly Inventario.Api.AplicacionApi _inventarioApp;
    private readonly Pedidos.Api.AplicacionApi _pedidosApp;
    private readonly HttpServer _productosServer;
    private readonly HttpServer _inventarioServer;
    private readonly HttpServer _pedidosServer;

    public HttpClient Productos { get; }
    public HttpClient Inventario { get; }
    public HttpClient Pedidos { get; }
    public Compuerta CompuertaProductos { get; } = new Compuerta();
    public Compuerta CompuertaInventario { get; } = new Compuerta();

    public Sistema()
    {
        _productosApp = global::Productos.Api.Composicion.Crear(_productosDb);
        _productosServer = new HttpServer(_productosApp.Configuracion);
        Productos = new HttpClient(_productosServer, false) { BaseAddress = new Uri("http://localhost/") };

        var productosParaInventario = new global::Inventario.Api.Clients.ProductosCliente(new HttpClient(new Interruptor(CompuertaProductos, _productosServer), false)
        {
            BaseAddress = new Uri("http://localhost/"),
            Timeout = TimeSpan.FromSeconds(2)
        });
        _inventarioApp = global::Inventario.Api.Composicion.Crear(_inventarioDb, productosParaInventario);
        _inventarioServer = new HttpServer(_inventarioApp.Configuracion);
        Inventario = new HttpClient(_inventarioServer, false) { BaseAddress = new Uri("http://localhost/") };

        var productosParaPedidos = new global::Pedidos.Api.Clients.ProductosCliente(new HttpClient(new Interruptor(CompuertaProductos, _productosServer), false)
        {
            BaseAddress = new Uri("http://localhost/"),
            Timeout = TimeSpan.FromSeconds(2)
        });
        var inventarioParaPedidos = new global::Pedidos.Api.Clients.InventarioCliente(new HttpClient(new Interruptor(CompuertaInventario, _inventarioServer), false)
        {
            BaseAddress = new Uri("http://localhost/"),
            Timeout = TimeSpan.FromSeconds(2)
        });
        _pedidosApp = global::Pedidos.Api.Composicion.Crear(_pedidosDb, productosParaPedidos, inventarioParaPedidos);
        _pedidosServer = new HttpServer(_pedidosApp.Configuracion);
        Pedidos = new HttpClient(_pedidosServer, false) { BaseAddress = new Uri("http://localhost/"), Timeout = TimeSpan.FromSeconds(8) };
    }

    public void Dispose()
    {
        Productos.Dispose();
        Inventario.Dispose();
        Pedidos.Dispose();
        _pedidosServer.Dispose();
        _inventarioServer.Dispose();
        _productosServer.Dispose();
        _pedidosApp.Dispose();
        _inventarioApp.Dispose();
        _productosApp.Dispose();
        Borrar(_productosDb);
        Borrar(_inventarioDb);
        Borrar(_pedidosDb);
    }

    private static void Borrar(string cadena)
    {
        try
        {
            using (var db = new global::Productos.Api.Data.ProductosContext(cadena))
                if (db.Database.Exists()) db.Database.Delete();
        }
        catch { }
        try
        {
            using (var db = new global::Inventario.Api.Data.InventarioContext(cadena))
                if (db.Database.Exists()) db.Database.Delete();
        }
        catch { }
        try
        {
            using (var db = new global::Pedidos.Api.Data.PedidosContext(cadena))
                if (db.Database.Exists()) db.Database.Delete();
        }
        catch { }
    }
}

[Collection("fw")]
public sealed class CalculoFrameworkTests
{
    [Fact]
    public void Subtotal_y_total_se_calculan_en_el_servidor()
    {
        var teclado = Pedidos.Api.Services.CalculoPedido.Subtotal(2, 15000m);
        var mouse = Pedidos.Api.Services.CalculoPedido.Subtotal(1, 15000m);
        Assert.Equal(30000m, teclado);
        Assert.Equal(45000m, Pedidos.Api.Services.CalculoPedido.Total(new[] { teclado, mouse }));
    }
}

[Collection("fw")]
public sealed class FrameworkComercialTests : IDisposable
{
    private readonly Sistema _sistema = new Sistema();

    [Fact]
    public async Task Swagger_es_2_y_el_catalogo_respeta_las_reglas()
    {
        var swagger = await _sistema.Productos.GetAsync("swagger/docs/v1");
        var texto = await swagger.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("\"swagger\":\"2.0\"", texto);
        Assert.Contains("/api/productos", texto);

        var categoria = await Post(_sistema.Productos, "api/categorias", new { nombre = "Periféricos", descripcion = "Accesorios" });
        Assert.Equal(HttpStatusCode.Created, categoria.Status);
        var id = (int)categoria.Json["id"];
        Assert.Equal(HttpStatusCode.Conflict, (await Post(_sistema.Productos, "api/categorias", new { nombre = "periféricos", descripcion = "otra" })).Status);

        var precio = await Post(_sistema.Productos, "api/productos", new { categoriaId = id, nombre = "Teclado", descripcion = "Mecánico", precio = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, precio.Status);
        var producto = await Post(_sistema.Productos, "api/productos", new { categoriaId = id, nombre = "Teclado", descripcion = "Mecánico", precio = 15000m });
        var productoId = (int)producto.Json["id"];
        var listado = await Get(_sistema.Productos, "api/productos?categoriaId=" + id);
        Assert.Equal(HttpStatusCode.OK, listado.Status);
        Assert.Contains(productoId, listado.Json.Select(p => (int)p["id"]));
        Assert.Equal(HttpStatusCode.NoContent, (await _sistema.Productos.DeleteAsync("api/categorias/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get(_sistema.Productos, "api/productos/" + productoId)).Status);
        var validacion = await Get(_sistema.Productos, "api/productos/" + productoId + "/validacion");
        Assert.Equal(HttpStatusCode.OK, validacion.Status);
        Assert.True((bool)validacion.Json["activo"]);
        Assert.False((bool)validacion.Json["categoriaActiva"]);
    }

    [Fact]
    public async Task Reserva_idempotente_sin_sobreventa_y_pedido_con_precio_historico()
    {
        var teclado = await Preparar("Teclados", "Teclado", 15000m, 10);
        var mouse = await Preparar("Mouses", "Mouse", 15000m, 1);
        var pedidoId = Guid.NewGuid();
        var reserva = await Post(_sistema.Inventario, "api/reservas", new { pedidoId, items = new[] { new { productoId = teclado, cantidad = 2 }, new { productoId = mouse, cantidad = 1 } } });
        Assert.Equal(HttpStatusCode.Created, reserva.Status);
        var repetida = await Post(_sistema.Inventario, "api/reservas", new { pedidoId, items = new[] { new { productoId = mouse, cantidad = 1 }, new { productoId = teclado, cantidad = 2 } } });
        Assert.Equal(HttpStatusCode.OK, repetida.Status);
        Assert.Equal((Guid)reserva.Json["id"], (Guid)repetida.Json["id"]);
        var otroContenido = await Post(_sistema.Inventario, "api/reservas", new { pedidoId, items = new[] { new { productoId = teclado, cantidad = 1 } } });
        Assert.Equal(HttpStatusCode.Conflict, otroContenido.Status);
        Assert.Equal(2, (int)(await Get(_sistema.Inventario, "api/inventario/" + teclado)).Json["stockReservado"]);
        var conflicto = await Post(_sistema.Inventario, "api/reservas", new { pedidoId = Guid.NewGuid(), items = new[] { new { productoId = mouse, cantidad = 5 } } });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.Status);
        Assert.Equal(1, (int)(await Get(_sistema.Inventario, "api/inventario/" + mouse)).Json["stockReservado"]);

        var creado = await Post(_sistema.Pedidos, "api/pedidos", new { items = new[] { new { productoId = teclado, cantidad = 2 } } });
        Assert.True(creado.Status == HttpStatusCode.Created, creado.Texto);
        Assert.Equal("CONFIRMADO", (string)creado.Json["estado"]);
        Assert.Equal(30000m, (decimal)creado.Json["total"]);
        var id = (Guid)creado.Json["id"];
        var cancelado = await Post(_sistema.Pedidos, "api/pedidos/" + id + "/cancelacion", new { });
        Assert.Equal(HttpStatusCode.OK, cancelado.Status);
        Assert.Equal("CANCELADO", (string)cancelado.Json["estado"]);
        var otra = await Post(_sistema.Pedidos, "api/pedidos/" + id + "/cancelacion", new { });
        Assert.Equal(HttpStatusCode.OK, otra.Status);
        Assert.Equal(8, (int)(await Get(_sistema.Inventario, "api/inventario/" + teclado)).Json["stockDisponible"]);

        var reservaId = (Guid)reserva.Json["id"];
        var liberada = await Post(_sistema.Inventario, "api/reservas/" + reservaId + "/liberacion", new { });
        Assert.Equal(HttpStatusCode.OK, liberada.Status);
        Assert.Equal(10, (int)(await Get(_sistema.Inventario, "api/inventario/" + teclado)).Json["stockDisponible"]);
        await Post(_sistema.Inventario, "api/reservas/" + reservaId + "/liberacion", new { });
        Assert.Equal(10, (int)(await Get(_sistema.Inventario, "api/inventario/" + teclado)).Json["stockDisponible"]);
    }

    [Fact]
    public async Task Dos_reservas_concurrentes_no_sobrevenden()
    {
        var producto = await Preparar("Sillas", "Silla", 50000m, 1);
        var primera = Post(_sistema.Inventario, "api/reservas", new { pedidoId = Guid.NewGuid(), items = new[] { new { productoId = producto, cantidad = 1 } } });
        var segunda = Post(_sistema.Inventario, "api/reservas", new { pedidoId = Guid.NewGuid(), items = new[] { new { productoId = producto, cantidad = 1 } } });
        var resultados = await Task.WhenAll(primera, segunda);
        var detalle = string.Join(" || ", resultados.Select(r => (int)r.Status + " " + r.Texto));
        Assert.True(resultados.Count(r => r.Status == HttpStatusCode.Created) == 1, detalle);
        Assert.True(resultados.Count(r => r.Status == HttpStatusCode.Conflict) == 1, detalle);
        Assert.Equal(0, (int)(await Get(_sistema.Inventario, "api/inventario/" + producto)).Json["stockDisponible"]);
    }

    [Fact]
    public async Task Falta_de_stock_timeout_y_reconciliacion()
    {
        var disco = await Preparar("Discos", "SSD", 20000m, 1);
        var rechazado = await Post(_sistema.Pedidos, "api/pedidos", new { items = new[] { new { productoId = disco, cantidad = 3 } } });
        Assert.Equal(HttpStatusCode.Conflict, rechazado.Status);
        Assert.Equal("RECHAZADO", (string)rechazado.Json["estado"]);
        Assert.Equal(1, (int)(await Get(_sistema.Inventario, "api/inventario/" + disco)).Json["stockDisponible"]);

        var fuente = await Preparar("Fuentes", "Fuente", 30000m, 5);
        _sistema.CompuertaInventario.SimularTimeout = true;
        Resp pendiente;
        try { pendiente = await Post(_sistema.Pedidos, "api/pedidos", new { items = new[] { new { productoId = fuente, cantidad = 2 } } }); }
        finally { _sistema.CompuertaInventario.Restablecer(); }
        Assert.True(pendiente.Status == HttpStatusCode.ServiceUnavailable, pendiente.Texto);
        Assert.Equal("PENDIENTE", (string)pendiente.Json["estado"]);
        var pedidoId = (Guid)pendiente.Json["pedidoId"];
        var conciliado = await Post(_sistema.Pedidos, "api/pedidos/reconciliacion", new { });
        Assert.Equal(1, (int)conciliado.Json["confirmados"]);
        Assert.Equal("CONFIRMADO", (string)(await Get(_sistema.Pedidos, "api/pedidos/" + pedidoId)).Json["estado"]);
        Assert.Equal(3, (int)(await Get(_sistema.Inventario, "api/inventario/" + fuente)).Json["stockDisponible"]);

        _sistema.CompuertaInventario.SimularTimeout = true;
        Resp cancelacion;
        try { cancelacion = await Post(_sistema.Pedidos, "api/pedidos/" + pedidoId + "/cancelacion", new { }); }
        finally { _sistema.CompuertaInventario.Restablecer(); }
        Assert.Equal("CANCELACION_PENDIENTE", (string)cancelacion.Json["estado"]);
        await Post(_sistema.Pedidos, "api/pedidos/reconciliacion", new { });
        Assert.Equal("CANCELADO", (string)(await Get(_sistema.Pedidos, "api/pedidos/" + pedidoId)).Json["estado"]);
        Assert.Equal(5, (int)(await Get(_sistema.Inventario, "api/inventario/" + fuente)).Json["stockDisponible"]);
        await Post(_sistema.Pedidos, "api/pedidos/" + pedidoId + "/cancelacion", new { });
        Assert.Equal(5, (int)(await Get(_sistema.Inventario, "api/inventario/" + fuente)).Json["stockDisponible"]);
    }

    [Fact]
    public async Task Productos_no_disponible_no_crea_el_pedido()
    {
        _sistema.CompuertaProductos.SimularNoDisponible = true;
        Resp respuesta;
        try
        {
            respuesta = await Post(_sistema.Pedidos, "api/pedidos", new { items = new[] { new { productoId = 1, cantidad = 1 } } });
        }
        finally
        {
            _sistema.CompuertaProductos.Restablecer();
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.Status);
        Assert.Null(respuesta.Json["pedidoId"]);
        var listado = await Get(_sistema.Pedidos, "api/pedidos");
        Assert.Empty(listado.Json);
    }

    private async Task<int> Preparar(string categoria, string nombre, decimal precio, int stock)
    {
        var creada = await Post(_sistema.Productos, "api/categorias", new { nombre = categoria + " " + Guid.NewGuid().ToString("N").Substring(0, 4), descripcion = categoria });
        Assert.True(creada.Status == HttpStatusCode.Created, creada.Texto);
        var categoriaId = (int)creada.Json["id"];
        var producto = await Post(_sistema.Productos, "api/productos", new { categoriaId, nombre, descripcion = nombre, precio });
        Assert.True(producto.Status == HttpStatusCode.Created, producto.Texto);
        var productoId = (int)producto.Json["id"];
        var alta = await Put(_sistema.Inventario, "api/inventario/" + productoId, new { stockTotal = stock });
        Assert.True(alta.Status == HttpStatusCode.OK, alta.Texto);
        return productoId;
    }

    public void Dispose() => _sistema.Dispose();

    private static Task<Resp> Get(HttpClient cliente, string ruta) => Leer(cliente.GetAsync(ruta));
    private static Task<Resp> Post(HttpClient cliente, string ruta, object cuerpo) =>
        Leer(cliente.PostAsync(ruta, new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json")));
    private static Task<Resp> Put(HttpClient cliente, string ruta, object cuerpo) =>
        Leer(cliente.PutAsync(ruta, new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json")));

    private static async Task<Resp> Leer(Task<HttpResponseMessage> tarea)
    {
        var respuesta = await tarea;
        var texto = await respuesta.Content.ReadAsStringAsync();
        JToken json = string.IsNullOrWhiteSpace(texto) ? null : JToken.Parse(texto);
        return new Resp(respuesta.StatusCode, json, texto);
    }
}

public sealed class Resp
{
    public Resp(HttpStatusCode status, JToken json, string texto)
    {
        Status = status;
        Json = json;
        Texto = texto;
    }
    public HttpStatusCode Status { get; }
    public JToken Json { get; }
    public string Texto { get; }
}
