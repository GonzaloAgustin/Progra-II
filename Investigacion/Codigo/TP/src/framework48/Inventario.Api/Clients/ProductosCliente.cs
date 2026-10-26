using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Inventario.Api.Infrastructure;
using Newtonsoft.Json;

namespace Inventario.Api.Clients;

public interface IProductosCliente
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
    public ConsultaEstado Estado { get; set; }
    public bool Activo { get; set; }
    public bool CategoriaActiva { get; set; }
}

public sealed class ProductosCliente : IProductosCliente
{
    private readonly HttpClient _http;

    public ProductosCliente(HttpClient http)
    {
        _http = http;
    }

    public async Task<ConsultaProducto> ObtenerAsync(int productoId, CancellationToken cancellationToken)
    {
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "api/productos/" + productoId + "/validacion"))
            {
                if (!string.IsNullOrWhiteSpace(Correlacion.Actual.Value))
                    request.Headers.TryAddWithoutValidation("X-Correlation-Id", Correlacion.Actual.Value);
                var response = await _http.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new ConsultaProducto { Estado = ConsultaEstado.NoEncontrado };
                if (!response.IsSuccessStatusCode)
                    return new ConsultaProducto { Estado = ConsultaEstado.NoDisponible };
                var json = await response.Content.ReadAsStringAsync();
                var cuerpo = JsonConvert.DeserializeObject<ValidacionProducto>(json);
                if (cuerpo == null)
                    return new ConsultaProducto { Estado = ConsultaEstado.NoDisponible };
                return new ConsultaProducto { Estado = ConsultaEstado.Ok, Activo = cuerpo.Activo, CategoriaActiva = cuerpo.CategoriaActiva };
            }
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConsultaProducto { Estado = ConsultaEstado.Timeout };
        }
        catch (HttpRequestException)
        {
            return new ConsultaProducto { Estado = ConsultaEstado.NoDisponible };
        }
    }

    private sealed class ValidacionProducto
    {
        public bool Activo { get; set; }
        public bool CategoriaActiva { get; set; }
    }
}
