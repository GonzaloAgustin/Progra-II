using Microsoft.AspNetCore.Mvc;
using Pedidos.Api.DTOs;
using Pedidos.Api.Infrastructure;
using Pedidos.Api.Services;

namespace Pedidos.Api.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController : ControllerBase
{
    private readonly IPedidoService _servicio;

    public PedidosController(IPedidoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista pedidos de todos los estados, con el precio histórico de cada ítem.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PedidoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ListarAsync(cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Detalle de un pedido.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ObtenerAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>
    /// Crea un pedido. El servidor consulta precios, guarda PENDIENTE y pide una reserva.
    /// 201 si queda CONFIRMADO. 409 si falta stock. 503 si el resultado de Inventario es incierto.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PedidoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Crear([FromBody] CrearPedidoDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(dto, cancellationToken);
        return ApiResults.From(this, resultado, creado =>
            CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado));
    }

    /// <summary>Cancela un pedido confirmado y libera la reserva. Repetir una cancelación completada responde 200 sin tocar el stock.</summary>
    [HttpPost("{id:guid}/cancelacion")]
    [ProducesResponseType(typeof(PedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CancelarAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Reintenta reservas y liberaciones que quedaron pendientes por un timeout. No duplica stock.</summary>
    [HttpPost("reconciliacion")]
    [ProducesResponseType(typeof(ReconciliacionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reconciliar(CancellationToken cancellationToken)
    {
        var resumen = await _servicio.ReconciliarAsync(cancellationToken);
        return Ok(resumen);
    }
}
