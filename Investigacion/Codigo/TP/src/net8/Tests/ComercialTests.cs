using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tests.Infraestructura;

namespace Tests;

[Collection("secuencial")]
public class ProductosTests : IClassFixture<ProductosApp>
{
    private readonly HttpClient _cliente;

    public ProductosTests(ProductosApp app)
    {
        _cliente = app.CreateClient();
    }

    [Fact]
    public async Task Swagger_publica_openapi_3()
    {
        var respuesta = await _cliente.GetAsync("/swagger/v1/swagger.json");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("\"openapi\": \"3.", cuerpo);
        Assert.Contains("/api/productos", cuerpo);
        Assert.Contains("/api/categorias", cuerpo);
        Assert.Contains("/validacion", cuerpo);
    }

    [Fact]
    public async Task Crud_de_categoria_filtro_y_baja_logica()
    {
        var creada = await Post("/api/categorias", new { nombre = "Periféricos", descripcion = "Accesorios para PC" });
        Assert.Equal(HttpStatusCode.Created, creada.Status);
        Assert.NotNull(creada.Headers.Location);
        var id = creada.Json.GetProperty("id").GetInt32();
        Assert.Equal("Periféricos", creada.Json.GetProperty("nombre").GetString());
        Assert.True(creada.Json.GetProperty("activo").GetBoolean());

        var duplicada = await Post("/api/categorias", new { nombre = "periféricos", descripcion = "otra" });
        Assert.Equal(HttpStatusCode.Conflict, duplicada.Status);

        var listado = await Get("/api/categorias");
        Assert.Contains(listado.Json.EnumerateArray(), c => c.GetProperty("id").GetInt32() == id);

        var actualizada = await Put($"/api/categorias/{id}", new { nombre = "Periféricos gamer", descripcion = "Teclados" });
        Assert.Equal(HttpStatusCode.OK, actualizada.Status);
        Assert.Equal("Periféricos gamer", actualizada.Json.GetProperty("nombre").GetString());

        var baja = await _cliente.DeleteAsync($"/api/categorias/{id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var oculta = await Get($"/api/categorias/{id}");
        Assert.Equal(HttpStatusCode.NotFound, oculta.Status);

        var listadoFinal = await Get("/api/categorias");
        Assert.DoesNotContain(listadoFinal.Json.EnumerateArray(), c => c.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task Crud_de_producto_precio_categoria_y_catalogo()
    {
        var perifericos = await Post("/api/categorias", new { nombre = "Audio", descripcion = "Sonido" });
        var otras = await Post("/api/categorias", new { nombre = "Cables", descripcion = "Conectores" });
        var categoriaId = perifericos.Json.GetProperty("id").GetInt32();
        var otraId = otras.Json.GetProperty("id").GetInt32();

        var invalido = await Post("/api/productos", new { categoriaId, nombre = "Parlante", descripcion = "Mini", precio = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.Status);

        var sinCategoria = await Post("/api/productos", new { categoriaId = 999999, nombre = "Parlante", descripcion = "Mini", precio = 1000 });
        Assert.Equal(HttpStatusCode.BadRequest, sinCategoria.Status);

        var creado = await Post("/api/productos", new { categoriaId, nombre = "Parlante", descripcion = "Mini", precio = 15000.00m });
        Assert.Equal(HttpStatusCode.Created, creado.Status);
        var productoId = creado.Json.GetProperty("id").GetInt32();
        Assert.Equal(15000.00m, creado.Json.GetProperty("precio").GetDecimal());
        Assert.Equal("Audio", creado.Json.GetProperty("categoria").GetProperty("nombre").GetString());

        var enOtra = await Get($"/api/productos?categoriaId={otraId}");
        Assert.DoesNotContain(enOtra.Json.EnumerateArray(), p => p.GetProperty("id").GetInt32() == productoId);

        var filtrado = await Get($"/api/productos?categoriaId={categoriaId}");
        Assert.Contains(filtrado.Json.EnumerateArray(), p => p.GetProperty("id").GetInt32() == productoId);

        var editado = await Put($"/api/productos/{productoId}", new { categoriaId = otraId, nombre = "Parlante pro", descripcion = "Grande", precio = 18000m });
        Assert.Equal(HttpStatusCode.OK, editado.Status);
        Assert.Equal(otraId, editado.Json.GetProperty("categoriaId").GetInt32());

        var baja = await _cliente.DeleteAsync($"/api/productos/{productoId}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get($"/api/productos/{productoId}")).Status);

        var validacion = await Get($"/api/productos/{productoId}/validacion");
        Assert.Equal(HttpStatusCode.OK, validacion.Status);
        Assert.False(validacion.Json.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public async Task Categoria_inactiva_oculta_sus_productos()
    {
        var categoria = await Post("/api/categorias", new { nombre = "Monitores", descripcion = "Pantallas" });
        var categoriaId = categoria.Json.GetProperty("id").GetInt32();
        var producto = await Post("/api/productos", new { categoriaId, nombre = "Monitor 24", descripcion = "IPS", precio = 90000m });
        var productoId = producto.Json.GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.NoContent, (await _cliente.DeleteAsync($"/api/categorias/{categoriaId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get($"/api/productos/{productoId}")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("/api/productos", new { categoriaId, nombre = "Monitor 27", descripcion = "IPS", precio = 120000m })).Status);
    }

    private async Task<Respuesta> Get(string ruta) => await Leer(await _cliente.GetAsync(ruta));
    private async Task<Respuesta> Post(string ruta, object cuerpo) => await Leer(await _cliente.PostAsJsonAsync(ruta, cuerpo));
    private async Task<Respuesta> Put(string ruta, object cuerpo) => await Leer(await _cliente.PutAsJsonAsync(ruta, cuerpo));

    private static async Task<Respuesta> Leer(HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return new Respuesta(respuesta.StatusCode, respuesta.Headers, json, texto);
    }
}

[Collection("secuencial")]
public class InventarioTests : IDisposable
{
    private readonly Sistema _sistema = new();
    private readonly HttpClient _productos;
    private readonly HttpClient _inventario;

    public InventarioTests()
    {
        _productos = _sistema.Productos.CreateClient();
        _inventario = _sistema.Inventario.CreateClient();
    }

    [Fact]
    public async Task Stock_reserva_liberacion_e_idempotencia()
    {
        var teclado = await CrearProducto("Teclados", "Teclado", 15000m);
        var mouse = await CrearProducto("Mouses", "Mouse", 8000m);

        Assert.Equal(HttpStatusCode.NotFound, (await Get(_inventario, "/api/inventario/999999")).Status);

        var alta = await Put(_inventario, $"/api/inventario/{teclado}", new { stockTotal = 50 });
        Assert.True(alta.Status == HttpStatusCode.OK, alta.Texto);
        Assert.Equal(50, alta.Json.GetProperty("stockDisponible").GetInt32());
        Assert.Equal(0, alta.Json.GetProperty("stockReservado").GetInt32());
        await Put(_inventario, $"/api/inventario/{mouse}", new { stockTotal = 1 });

        var pedidoId = Guid.NewGuid();
        var reserva = await Post(_inventario, "/api/reservas", new
        {
            pedidoId,
            items = new[] { new { productoId = teclado, cantidad = 2 }, new { productoId = mouse, cantidad = 1 } }
        });
        Assert.Equal(HttpStatusCode.Created, reserva.Status);
        Assert.Equal("ACTIVA", reserva.Json.GetProperty("estado").GetString());
        var reservaId = reserva.Json.GetProperty("id").GetGuid();

        var repetida = await Post(_inventario, "/api/reservas", new
        {
            pedidoId,
            items = new[] { new { productoId = mouse, cantidad = 1 }, new { productoId = teclado, cantidad = 2 } }
        });
        Assert.Equal(HttpStatusCode.OK, repetida.Status);
        Assert.Equal(reservaId, repetida.Json.GetProperty("id").GetGuid());
        Assert.Equal(2, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockReservado").GetInt32());

        var conflicto = await Post(_inventario, "/api/reservas", new
        {
            pedidoId,
            items = new[] { new { productoId = teclado, cantidad = 9 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.Status);

        var sinStock = await Post(_inventario, "/api/reservas", new
        {
            pedidoId = Guid.NewGuid(),
            items = new[] { new { productoId = teclado, cantidad = 1 }, new { productoId = mouse, cantidad = 5 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, sinStock.Status);
        Assert.Equal(2, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockReservado").GetInt32());
        Assert.Equal(1, (await Get(_inventario, $"/api/inventario/{mouse}")).Json.GetProperty("stockReservado").GetInt32());

        var bajoMinimo = await Put(_inventario, $"/api/inventario/{teclado}", new { stockTotal = 1 });
        Assert.Equal(HttpStatusCode.Conflict, bajoMinimo.Status);

        var libre = await Post(_inventario, $"/api/reservas/{reservaId}/liberacion", new { });
        Assert.Equal(HttpStatusCode.OK, libre.Status);
        Assert.Equal("LIBERADA", libre.Json.GetProperty("estado").GetString());
        Assert.Equal(0, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockReservado").GetInt32());

        var otraVez = await Post(_inventario, $"/api/reservas/{reservaId}/liberacion", new { });
        Assert.Equal(HttpStatusCode.OK, otraVez.Status);
        Assert.Equal(0, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockReservado").GetInt32());
        Assert.Equal(50, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockDisponible").GetInt32());
    }

    [Fact]
    public async Task No_carga_stock_si_el_producto_no_esta_activo_o_productos_no_responde()
    {
        var categoria = await Post(_productos, "/api/categorias", new { nombre = "Redes", descripcion = "Lan" });
        var categoriaId = categoria.Json.GetProperty("id").GetInt32();
        var producto = await Post(_productos, "/api/productos", new { categoriaId, nombre = "Switch", descripcion = "8 puertos", precio = 40000m });
        var productoId = producto.Json.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await _productos.DeleteAsync($"/api/productos/{productoId}")).StatusCode);

        var inactivo = await Put(_inventario, $"/api/inventario/{productoId}", new { stockTotal = 4 });
        Assert.Equal(HttpStatusCode.Conflict, inactivo.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(_inventario, "/api/inventario/424242", new { stockTotal = 4 })).Status);

        _sistema.CompuertaProductos.SimularNoDisponible = true;
        try
        {
            var caido = await Put(_inventario, $"/api/inventario/{productoId}", new { stockTotal = 4 });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, caido.Status);
        }
        finally
        {
            _sistema.CompuertaProductos.Restablecer();
        }
    }

    [Fact]
    public async Task Dos_reservas_concurrentes_no_sobrevenden()
    {
        var productoId = await CrearProducto("Sillas", "Silla", 50000m);
        Assert.Equal(HttpStatusCode.OK, (await Put(_inventario, $"/api/inventario/{productoId}", new { stockTotal = 1 })).Status);

        var primera = Post(_inventario, "/api/reservas", new { pedidoId = Guid.NewGuid(), items = new[] { new { productoId, cantidad = 1 } } });
        var segunda = Post(_inventario, "/api/reservas", new { pedidoId = Guid.NewGuid(), items = new[] { new { productoId, cantidad = 1 } } });
        var resultados = await Task.WhenAll(primera, segunda);

        var detalle = string.Join(" || ", resultados.Select(r => $"{(int)r.Status} {r.Texto}"));
        Assert.True(resultados.Count(r => r.Status == HttpStatusCode.Created) == 1, detalle);
        Assert.True(resultados.Count(r => r.Status == HttpStatusCode.Conflict) == 1, detalle);

        var stock = await Get(_inventario, $"/api/inventario/{productoId}");
        Assert.Equal(1, stock.Json.GetProperty("stockReservado").GetInt32());
        Assert.Equal(0, stock.Json.GetProperty("stockDisponible").GetInt32());
    }

    private async Task<int> CrearProducto(string categoria, string nombre, decimal precio)
    {
        var creada = await Post(_productos, "/api/categorias", new { nombre = categoria + Guid.NewGuid().ToString("N")[..6], descripcion = categoria });
        var categoriaId = creada.Json.GetProperty("id").GetInt32();
        var producto = await Post(_productos, "/api/productos", new { categoriaId, nombre, descripcion = nombre, precio });
        Assert.Equal(HttpStatusCode.Created, producto.Status);
        return producto.Json.GetProperty("id").GetInt32();
    }

    public void Dispose() => _sistema.Dispose();

    private static async Task<Respuesta> Get(HttpClient cliente, string ruta) => await Leer(await cliente.GetAsync(ruta));
    private static async Task<Respuesta> Post(HttpClient cliente, string ruta, object cuerpo) => await Leer(await cliente.PostAsJsonAsync(ruta, cuerpo));
    private static async Task<Respuesta> Put(HttpClient cliente, string ruta, object cuerpo) => await Leer(await cliente.PutAsJsonAsync(ruta, cuerpo));

    private static async Task<Respuesta> Leer(HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return new Respuesta(respuesta.StatusCode, respuesta.Headers, json, texto);
    }
}

[Collection("secuencial")]
public class PedidosTests : IDisposable
{
    private readonly Sistema _sistema = new();
    private readonly HttpClient _productos;
    private readonly HttpClient _inventario;
    private readonly HttpClient _pedidos;

    public PedidosTests()
    {
        _productos = _sistema.Productos.CreateClient();
        _inventario = _sistema.Inventario.CreateClient();
        _pedidos = _sistema.Pedidos.CreateClient();
    }

    [Fact]
    public async Task Crea_pedido_con_precio_historico_y_cancela_sin_duplicar_la_liberacion()
    {
        var teclado = await Preparar("Periféricos pedido", "Teclado mecánico", 15000m, 10);
        var mouse = await Preparar("Mouses pedido", "Mouse", 15000m, 10);

        var creado = await Post(_pedidos, "/api/pedidos", new
        {
            items = new[]
            {
                new { productoId = teclado, cantidad = 2 },
                new { productoId = mouse, cantidad = 1 }
            }
        });
        Assert.Equal(HttpStatusCode.Created, creado.Status);
        Assert.Equal("CONFIRMADO", creado.Json.GetProperty("estado").GetString());
        Assert.Equal(45000.00m, creado.Json.GetProperty("total").GetDecimal());
        var items = creado.Json.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(30000.00m, items[0].GetProperty("subtotal").GetDecimal());
        Assert.Equal(15000.00m, items[1].GetProperty("subtotal").GetDecimal());
        Assert.Equal(8, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockDisponible").GetInt32());

        var id = creado.Json.GetProperty("id").GetGuid();
        var cancelado = await Post(_pedidos, $"/api/pedidos/{id}/cancelacion", new { });
        Assert.Equal(HttpStatusCode.OK, cancelado.Status);
        Assert.Equal("CANCELADO", cancelado.Json.GetProperty("estado").GetString());
        Assert.Equal(10, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockDisponible").GetInt32());

        var repetida = await Post(_pedidos, $"/api/pedidos/{id}/cancelacion", new { });
        Assert.Equal(HttpStatusCode.OK, repetida.Status);
        Assert.Equal("CANCELADO", repetida.Json.GetProperty("estado").GetString());
        Assert.Equal(10, (await Get(_inventario, $"/api/inventario/{teclado}")).Json.GetProperty("stockDisponible").GetInt32());
    }

    [Fact]
    public async Task Rechaza_el_pedido_completo_si_falta_stock()
    {
        var producto = await Preparar("Discos", "SSD", 20000m, 1);
        var rechazado = await Post(_pedidos, "/api/pedidos", new
        {
            items = new[] { new { productoId = producto, cantidad = 3 } }
        });

        Assert.Equal(HttpStatusCode.Conflict, rechazado.Status);
        Assert.Equal("RECHAZADO", rechazado.Json.GetProperty("estado").GetString());
        var pedidoId = rechazado.Json.GetProperty("pedidoId").GetGuid();
        var consultado = await Get(_pedidos, $"/api/pedidos/{pedidoId}");
        Assert.Equal("RECHAZADO", consultado.Json.GetProperty("estado").GetString());
        Assert.Equal(1, (await Get(_inventario, $"/api/inventario/{producto}")).Json.GetProperty("stockDisponible").GetInt32());
    }

    [Fact]
    public async Task Timeout_deja_el_pedido_pendiente_y_la_reconciliacion_lo_confirma_una_sola_vez()
    {
        var producto = await Preparar("Fuentes", "Fuente 650", 30000m, 5);
        _sistema.CompuertaInventario.SimularTimeout = true;
        Respuesta creado;
        try
        {
            creado = await Post(_pedidos, "/api/pedidos", new { items = new[] { new { productoId = producto, cantidad = 2 } } });
        }
        finally
        {
            _sistema.CompuertaInventario.Restablecer();
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, creado.Status);
        Assert.Equal("PENDIENTE", creado.Json.GetProperty("estado").GetString());
        var pedidoId = creado.Json.GetProperty("pedidoId").GetGuid();
        Assert.Equal(5, (await Get(_inventario, $"/api/inventario/{producto}")).Json.GetProperty("stockDisponible").GetInt32());

        var reconciliacion = await Post(_pedidos, "/api/pedidos/reconciliacion", new { });
        Assert.Equal(HttpStatusCode.OK, reconciliacion.Status);
        Assert.Equal(1, reconciliacion.Json.GetProperty("confirmados").GetInt32());

        var pedido = await Get(_pedidos, $"/api/pedidos/{pedidoId}");
        Assert.Equal("CONFIRMADO", pedido.Json.GetProperty("estado").GetString());
        Assert.Equal(3, (await Get(_inventario, $"/api/inventario/{producto}")).Json.GetProperty("stockDisponible").GetInt32());

        await Post(_pedidos, "/api/pedidos/reconciliacion", new { });
        Assert.Equal(3, (await Get(_inventario, $"/api/inventario/{producto}")).Json.GetProperty("stockDisponible").GetInt32());
    }

    [Fact]
    public async Task Timeout_de_cancelacion_se_recupera_sin_liberar_dos_veces()
    {
        var producto = await Preparar("Gabinetes", "Gabinete", 25000m, 4);
        var creado = await Post(_pedidos, "/api/pedidos", new { items = new[] { new { productoId = producto, cantidad = 1 } } });
        var pedidoId = creado.Json.GetProperty("id").GetGuid();

        _sistema.CompuertaInventario.SimularTimeout = true;
        Respuesta cancelacion;
        try
        {
            cancelacion = await Post(_pedidos, $"/api/pedidos/{pedidoId}/cancelacion", new { });
        }
        finally
        {
            _sistema.CompuertaInventario.Restablecer();
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, cancelacion.Status);
        Assert.Equal("CANCELACION_PENDIENTE", cancelacion.Json.GetProperty("estado").GetString());
        Assert.Equal(3, (await Get(_inventario, $"/api/inventario/{producto}")).Json.GetProperty("stockDisponible").GetInt32());

        await Post(_pedidos, "/api/pedidos/reconciliacion", new { });
        Assert.Equal("CANCELADO", (await Get(_pedidos, $"/api/pedidos/{pedidoId}")).Json.GetProperty("estado").GetString());
        Assert.Equal(4, (await Get(_inventario, $"/api/inventario/{producto}")).Json.GetProperty("stockDisponible").GetInt32());

        var segunda = await Post(_pedidos, $"/api/pedidos/{pedidoId}/cancelacion", new { });
        Assert.Equal(HttpStatusCode.OK, segunda.Status);
        Assert.Equal(4, (await Get(_inventario, $"/api/inventario/{producto}")).Json.GetProperty("stockDisponible").GetInt32());
    }

    [Fact]
    public async Task Productos_caido_no_crea_el_pedido()
    {
        _sistema.CompuertaProductos.SimularNoDisponible = true;
        try
        {
            var respuesta = await Post(_pedidos, "/api/pedidos", new { items = new[] { new { productoId = 1, cantidad = 1 } } });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.Status);
            Assert.False(respuesta.Json.TryGetProperty("pedidoId", out _));
        }
        finally
        {
            _sistema.CompuertaProductos.Restablecer();
        }

        var listado = await Get(_pedidos, "/api/pedidos");
        Assert.Empty(listado.Json.EnumerateArray());
    }

    [Fact]
    public async Task Swagger_de_cada_servicio_responde()
    {
        Assert.Equal(HttpStatusCode.OK, (await _productos.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _inventario.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        var pedidos = await _pedidos.GetAsync("/swagger/v1/swagger.json");
        var cuerpo = await pedidos.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, pedidos.StatusCode);
        Assert.Contains("/api/pedidos/{id}/cancelacion", cuerpo);
        Assert.Contains("reconciliacion", cuerpo);
    }

    private async Task<int> Preparar(string categoria, string nombre, decimal precio, int stock)
    {
        var creada = await Post(_productos, "/api/categorias", new { nombre = categoria + " " + Guid.NewGuid().ToString("N")[..4], descripcion = categoria });
        var categoriaId = creada.Json.GetProperty("id").GetInt32();
        var producto = await Post(_productos, "/api/productos", new { categoriaId, nombre, descripcion = nombre, precio });
        var productoId = producto.Json.GetProperty("id").GetInt32();
        var alta = await Put(_inventario, $"/api/inventario/{productoId}", new { stockTotal = stock });
        Assert.True(alta.Status == HttpStatusCode.OK, alta.Texto);
        return productoId;
    }

    public void Dispose() => _sistema.Dispose();

    private static async Task<Respuesta> Get(HttpClient cliente, string ruta) => await Leer(await cliente.GetAsync(ruta));
    private static async Task<Respuesta> Post(HttpClient cliente, string ruta, object cuerpo) => await Leer(await cliente.PostAsJsonAsync(ruta, cuerpo));
    private static async Task<Respuesta> Put(HttpClient cliente, string ruta, object cuerpo) => await Leer(await cliente.PutAsJsonAsync(ruta, cuerpo));

    private static async Task<Respuesta> Leer(HttpResponseMessage respuesta)
    {
        var texto = await respuesta.Content.ReadAsStringAsync();
        var json = string.IsNullOrWhiteSpace(texto) ? default : JsonDocument.Parse(texto).RootElement.Clone();
        return new Respuesta(respuesta.StatusCode, respuesta.Headers, json, texto);
    }
}

public readonly record struct Respuesta(HttpStatusCode Status, System.Net.Http.Headers.HttpResponseHeaders Headers, JsonElement Json, string Texto);
