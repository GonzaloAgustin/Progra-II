using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Pedidos.Api.DTOs;
using Pedidos.Api.Infrastructure;
using Pedidos.Api.Services;

namespace Pedidos.Api.Controllers;

[RoutePrefix("api/pedidos")]
public class PedidosController : ApiController
{
    private readonly IPedidoService _servicio;

    public PedidosController(IPedidoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista los pedidos.</summary>
    [HttpGet, Route("")]
    [ResponseType(typeof(IEnumerable<PedidoDto>))]
    public async Task<IHttpActionResult> Listar(CancellationToken cancellationToken) =>
        ApiResults.Desde(this, await _servicio.ListarAsync(cancellationToken));

    /// <summary>Obtiene un pedido por id.</summary>
    [HttpGet, Route("{id:guid}")]
    [ResponseType(typeof(PedidoDto))]
    public async Task<IHttpActionResult> Obtener(Guid id, CancellationToken cancellationToken) =>
        ApiResults.Desde(this, await _servicio.ObtenerAsync(id, cancellationToken));

    /// <summary>Crea el pedido. El precio sale de Productos. Ejemplo: {"items":[{"productoId":1,"cantidad":2}]}. 201 confirmado, 409 sin stock, 503 si Inventario no responde.</summary>
    [HttpPost, Route("")]
    [ResponseType(typeof(PedidoDto))]
    public async Task<IHttpActionResult> Crear([FromBody] CrearPedidoDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(dto, cancellationToken);
        return ApiResults.Desde(this, resultado, creado => Created(new Uri(Request.RequestUri, creado.Id.ToString()), creado));
    }

    /// <summary>Cancela un pedido confirmado y libera la reserva. Repetir una cancelación completada responde 200.</summary>
    [HttpPost, Route("{id:guid}/cancelacion")]
    [ResponseType(typeof(PedidoDto))]
    public async Task<IHttpActionResult> Cancelar(Guid id, CancellationToken cancellationToken) =>
        ApiResults.Desde(this, await _servicio.CancelarAsync(id, cancellationToken));

    /// <summary>Reintenta reservas y liberaciones pendientes sin duplicar stock.</summary>
    [HttpPost, Route("reconciliacion")]
    public async Task<IHttpActionResult> Reconciliar(CancellationToken cancellationToken)
    {
        var resumen = await _servicio.ReconciliarAsync(cancellationToken);
        return Ok(resumen);
    }
}
