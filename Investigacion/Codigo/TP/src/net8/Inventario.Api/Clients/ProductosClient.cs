using System.Net;
using System.Net.Http.Json;

namespace Inventario.Api.Clients;

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
    public bool Activo { get; init; }
    public bool CategoriaActiva { get; init; }

    public static ConsultaProducto Encontrado(bool activo, bool categoriaActiva) =>
        new() { Estado = ConsultaEstado.Ok, Activo = activo, CategoriaActiva = categoriaActiva };

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
            if (cuerpo is null)
                return ConsultaProducto.NoDisponible();

            return ConsultaProducto.Encontrado(cuerpo.Activo, cuerpo.CategoriaActiva);
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

    private sealed record ValidacionProducto(int Id, bool Activo, bool CategoriaActiva);
}
