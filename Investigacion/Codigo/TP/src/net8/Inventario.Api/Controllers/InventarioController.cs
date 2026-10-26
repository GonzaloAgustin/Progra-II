using Inventario.Api.DTOs;
using Inventario.Api.Infrastructure;
using Inventario.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/inventario")]
public class InventarioController : ControllerBase
{
    private readonly IInventarioService _servicio;

    public InventarioController(IInventarioService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Consulta stock total, reservado y disponible.</summary>
    [HttpGet("{productoId:int}")]
    [ProducesResponseType(typeof(StockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int productoId, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ObtenerAsync(productoId, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Fija el stock total. Crea la fila si no existe y el producto está activo. No incrementa: reemplaza el total.</summary>
    [HttpPut("{productoId:int}")]
    [ProducesResponseType(typeof(StockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Establecer(int productoId, [FromBody] EstablecerStockDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.EstablecerTotalAsync(productoId, dto, cancellationToken);
        return ApiResults.From(this, resultado);
    }
}
