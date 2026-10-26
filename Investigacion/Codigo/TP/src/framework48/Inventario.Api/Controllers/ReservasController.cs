using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Inventario.Api.DTOs;
using Inventario.Api.Infrastructure;
using Inventario.Api.Services;

namespace Inventario.Api.Controllers;

[RoutePrefix("api/reservas")]
public class ReservasController : ApiController
{
    private readonly IInventarioService _servicio;

    public ReservasController(IInventarioService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Reserva atómica e idempotente por pedidoId. 201 la primera vez, 200 si se repite igual, 409 si el contenido cambia o falta stock.</summary>
    [HttpPost, Route("")]
    [ResponseType(typeof(ReservaDto))]
    public async Task<IHttpActionResult> Reservar([FromBody] CrearReservaDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ReservarAsync(dto, cancellationToken);
        return ApiResults.Desde(this, resultado, creado => Created(new Uri(Request.RequestUri, creado.Id.ToString()), creado));
    }

    /// <summary>Libera la reserva. Repetir la liberación responde 200 y no devuelve el stock dos veces.</summary>
    [HttpPost, Route("{id:guid}/liberacion")]
    [ResponseType(typeof(ReservaDto))]
    public async Task<IHttpActionResult> Liberar(Guid id, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.LiberarAsync(id, cancellationToken));
    }
}
