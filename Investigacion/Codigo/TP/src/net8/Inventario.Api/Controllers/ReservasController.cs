using Inventario.Api.DTOs;
using Inventario.Api.Infrastructure;
using Inventario.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/reservas")]
public class ReservasController : ControllerBase
{
    private readonly IInventarioService _servicio;

    public ReservasController(IInventarioService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Reserva varios productos en una sola transacción. El mismo pedidoId con los mismos ítems no descuenta dos veces.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ReservaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reservar([FromBody] CrearReservaDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ReservarAsync(dto, cancellationToken);
        return ApiResults.From(this, resultado, creado =>
            Created($"/api/reservas/{creado.Id}", creado));
    }

    /// <summary>Libera la reserva. Repetir la operación responde 200 y no devuelve stock otra vez.</summary>
    [HttpPost("{id:guid}/liberacion")]
    [ProducesResponseType(typeof(ReservaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Liberar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.LiberarAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }
}
