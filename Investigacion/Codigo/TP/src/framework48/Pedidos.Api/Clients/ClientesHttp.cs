using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pedidos.Api.Infrastructure;

namespace Pedidos.Api.Clients;

public enum ConsultaEstado { Ok, NoEncontrado, NoDisponible, Timeout }
public enum ResultadoRemoto { Confirmada, StockInsuficiente, NoEncontrada, NoDisponible, Timeout }

public sealed class ConsultaProducto
{
    public ConsultaEstado Estado { get; set; }
    public string Nombre { get; set; }
    public decimal Precio { get; set; }
    public bool Activo { get; set; }
    public bool CategoriaActiva { get; set; }
}

public sealed class ResultadoReserva
{
    public ResultadoRemoto Estado { get; set; }
    public Guid ReservaId { get; set; }
}

public sealed class ResultadoLiberacion
{
    public ResultadoRemoto Estado { get; set; }
}

public interface IProductosCliente
{
    Task<ConsultaProducto> ObtenerAsync(int productoId, CancellationToken cancellationToken);
}

public interface IInventarioCliente
{
    Task<ResultadoReserva> ReservarAsync(Guid pedidoId, IReadOnlyList<Tuple<int, int>> items, CancellationToken cancellationToken);
    Task<ResultadoLiberacion> LiberarAsync(Guid reservaId, CancellationToken cancellationToken);
}

public sealed class ProductosCliente : IProductosCliente
{
    private readonly HttpClient _http;

    public ProductosCliente(HttpClient http) { _http = http; }

    public async Task<ConsultaProducto> ObtenerAsync(int productoId, CancellationToken cancellationToken)
    {
        try
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "api/productos/" + productoId + "/validacion"))
            {
                AgregarCorrelacion(request);
                var response = await _http.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return new ConsultaProducto { Estado = ConsultaEstado.NoEncontrado };
                if (!response.IsSuccessStatusCode)
                    return new ConsultaProducto { Estado = ConsultaEstado.NoDisponible };
                var cuerpo = JsonConvert.DeserializeObject<ValidacionProducto>(await response.Content.ReadAsStringAsync());
                if (cuerpo == null || cuerpo.Precio <= 0)
                    return new ConsultaProducto { Estado = ConsultaEstado.NoDisponible };
                return new ConsultaProducto
                {
                    Estado = ConsultaEstado.Ok,
                    Nombre = cuerpo.Nombre,
                    Precio = cuerpo.Precio,
                    Activo = cuerpo.Activo,
                    CategoriaActiva = cuerpo.CategoriaActiva
                };
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

    private static void AgregarCorrelacion(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(Correlacion.Actual.Value))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", Correlacion.Actual.Value);
    }

    private sealed class ValidacionProducto
    {
        public string Nombre { get; set; }
        public decimal Precio { get; set; }
        public bool Activo { get; set; }
        public bool CategoriaActiva { get; set; }
    }
}

public sealed class InventarioCliente : IInventarioCliente
{
    private readonly HttpClient _http;

    public InventarioCliente(HttpClient http) { _http = http; }

    public async Task<ResultadoReserva> ReservarAsync(Guid pedidoId, IReadOnlyList<Tuple<int, int>> items, CancellationToken cancellationToken)
    {
        var cuerpo = new
        {
            pedidoId,
            items = items.Select(i => new { productoId = i.Item1, cantidad = i.Item2 }).ToList()
        };
        try
        {
            var response = await Enviar(HttpMethod.Post, "api/reservas", cuerpo, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
            {
                var reserva = JsonConvert.DeserializeObject<ReservaRemota>(await response.Content.ReadAsStringAsync());
                if (reserva == null || reserva.Estado != "ACTIVA" || reserva.Id == Guid.Empty)
                    return new ResultadoReserva { Estado = ResultadoRemoto.NoDisponible };
                return new ResultadoReserva { Estado = ResultadoRemoto.Confirmada, ReservaId = reserva.Id };
            }
            if (response.StatusCode == HttpStatusCode.Conflict)
                return new ResultadoReserva { Estado = ResultadoRemoto.StockInsuficiente };
            return new ResultadoReserva { Estado = ResultadoRemoto.NoDisponible };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ResultadoReserva { Estado = ResultadoRemoto.Timeout };
        }
        catch (HttpRequestException)
        {
            return new ResultadoReserva { Estado = ResultadoRemoto.NoDisponible };
        }
    }

    public async Task<ResultadoLiberacion> LiberarAsync(Guid reservaId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await Enviar(HttpMethod.Post, "api/reservas/" + reservaId + "/liberacion", null, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var reserva = JsonConvert.DeserializeObject<ReservaRemota>(await response.Content.ReadAsStringAsync());
                return new ResultadoLiberacion
                {
                    Estado = reserva != null && reserva.Estado == "LIBERADA" ? ResultadoRemoto.Confirmada : ResultadoRemoto.NoDisponible
                };
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new ResultadoLiberacion { Estado = ResultadoRemoto.NoEncontrada };
            return new ResultadoLiberacion { Estado = ResultadoRemoto.NoDisponible };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ResultadoLiberacion { Estado = ResultadoRemoto.Timeout };
        }
        catch (HttpRequestException)
        {
            return new ResultadoLiberacion { Estado = ResultadoRemoto.NoDisponible };
        }
    }

    private async Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, object cuerpo, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(metodo, ruta);
        if (!string.IsNullOrWhiteSpace(Correlacion.Actual.Value))
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", Correlacion.Actual.Value);
        if (cuerpo != null)
            request.Content = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
        return await _http.SendAsync(request, cancellationToken);
    }

    private sealed class ReservaRemota
    {
        public Guid Id { get; set; }
        public string Estado { get; set; }
    }
}
