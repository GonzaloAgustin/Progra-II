using System.Net;
using System.Net.Http.Json;

namespace Pedidos.Api.Clients;

public interface IProductosClient
{
    Task<ConsultaProducto> ObtenerAsync(int productoId, CancellationToken cancellationToken);
}

public enum ConsultaEstado
{
    Ok,
    NoEncontrado,
    NoDisponible,
    Timeout
}

public sealed class ConsultaProducto
{
    public ConsultaEstado Estado { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public decimal Precio { get; init; }
    public bool Activo { get; init; }
    public bool CategoriaActiva { get; init; }

    public static ConsultaProducto Encontrado(string nombre, decimal precio, bool activo, bool categoriaActiva) =>
        new() { Estado = ConsultaEstado.Ok, Nombre = nombre, Precio = precio, Activo = activo, CategoriaActiva = categoriaActiva };

    public static ConsultaProducto NoEncontrado() => new() { Estado = ConsultaEstado.NoEncontrado };
    public static ConsultaProducto NoDisponible() => new() { Estado = ConsultaEstado.NoDisponible };
    public static ConsultaProducto TiempoAgotado() => new() { Estado = ConsultaEstado.Timeout };
}

public sealed class ProductosClient : IProductosClient
{
    private readonly HttpClient _http;

    public ProductosClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ConsultaProducto> ObtenerAsync(int productoId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync($"/api/productos/{productoId}/validacion", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return ConsultaProducto.NoEncontrado();
            if (!response.IsSuccessStatusCode)
                return ConsultaProducto.NoDisponible();

            var cuerpo = await response.Content.ReadFromJsonAsync<ValidacionProducto>(cancellationToken);
            if (cuerpo is null || cuerpo.Precio <= 0)
                return ConsultaProducto.NoDisponible();

            return ConsultaProducto.Encontrado(cuerpo.Nombre, cuerpo.Precio, cuerpo.Activo, cuerpo.CategoriaActiva);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConsultaProducto.TiempoAgotado();
        }
        catch (HttpRequestException)
        {
            return ConsultaProducto.NoDisponible();
        }
    }

    private sealed record ValidacionProducto(int Id, string Nombre, decimal Precio, bool Activo, bool CategoriaActiva);
}

public interface IInventarioClient
{
    Task<ResultadoReserva> ReservarAsync(Guid pedidoId, IReadOnlyList<(int ProductoId, int Cantidad)> items, CancellationToken cancellationToken);
    Task<ResultadoLiberacion> LiberarAsync(Guid reservaId, CancellationToken cancellationToken);
}

public enum ResultadoRemoto
{
    Confirmada,
    StockInsuficiente,
    NoEncontrada,
    NoDisponible,
    Timeout
}

public sealed class ResultadoReserva
{
    public ResultadoRemoto Estado { get; init; }
    public Guid ReservaId { get; init; }

    public static ResultadoReserva Confirmada(Guid reservaId) => new() { Estado = ResultadoRemoto.Confirmada, ReservaId = reservaId };
    public static ResultadoReserva StockInsuficiente() => new() { Estado = ResultadoRemoto.StockInsuficiente };
    public static ResultadoReserva NoDisponible() => new() { Estado = ResultadoRemoto.NoDisponible };
    public static ResultadoReserva TiempoAgotado() => new() { Estado = ResultadoRemoto.Timeout };
}

public sealed class ResultadoLiberacion
{
    public ResultadoRemoto Estado { get; init; }

    public static ResultadoLiberacion Confirmada() => new() { Estado = ResultadoRemoto.Confirmada };
    public static ResultadoLiberacion NoEncontrada() => new() { Estado = ResultadoRemoto.NoEncontrada };
    public static ResultadoLiberacion NoDisponible() => new() { Estado = ResultadoRemoto.NoDisponible };
    public static ResultadoLiberacion TiempoAgotado() => new() { Estado = ResultadoRemoto.Timeout };
}

public sealed class InventarioClient : IInventarioClient
{
    private readonly HttpClient _http;

    public InventarioClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<ResultadoReserva> ReservarAsync(Guid pedidoId, IReadOnlyList<(int ProductoId, int Cantidad)> items, CancellationToken cancellationToken)
    {
        var cuerpo = new
        {
            pedidoId,
            items = items.Select(i => new { productoId = i.ProductoId, cantidad = i.Cantidad }).ToList()
        };

        try
        {
            var response = await _http.PostAsJsonAsync("/api/reservas", cuerpo, cancellationToken);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                var reserva = await response.Content.ReadFromJsonAsync<ReservaRemota>(cancellationToken);
                if (reserva is null || reserva.Estado != "ACTIVA" || reserva.Id == Guid.Empty)
                    return ResultadoReserva.NoDisponible();
                return ResultadoReserva.Confirmada(reserva.Id);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
                return ResultadoReserva.StockInsuficiente();

            return ResultadoReserva.NoDisponible();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoReserva.TiempoAgotado();
        }
        catch (HttpRequestException)
        {
            return ResultadoReserva.NoDisponible();
        }
    }

    public async Task<ResultadoLiberacion> LiberarAsync(Guid reservaId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.PostAsync($"/api/reservas/{reservaId}/liberacion", content: null, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var reserva = await response.Content.ReadFromJsonAsync<ReservaRemota>(cancellationToken);
                return reserva?.Estado == "LIBERADA"
                    ? ResultadoLiberacion.Confirmada()
                    : ResultadoLiberacion.NoDisponible();
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                return ResultadoLiberacion.NoEncontrada();

            return ResultadoLiberacion.NoDisponible();
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoLiberacion.TiempoAgotado();
        }
        catch (HttpRequestException)
        {
            return ResultadoLiberacion.NoDisponible();
        }
    }

    private sealed record ReservaRemota(Guid Id, string Estado);
}
