using Microsoft.AspNetCore.Mvc;
using Productos.Api.DTOs;
using Productos.Api.Infrastructure;
using Productos.Api.Services;

namespace Productos.Api.Controllers;

[ApiController]
[Route("api/productos")]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _servicio;

    public ProductosController(IProductoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista productos activos de categorías activas. Filtra con categoriaId.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] int? categoriaId, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ListarAsync(categoriaId, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Detalle público. Responde 404 si el producto no existe o no está visible.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ObtenerAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Contrato interno para Inventario y Pedidos. Distingue inexistente de inactivo y devuelve el precio vigente.</summary>
    [HttpGet("{id:int}/validacion")]
    [ProducesResponseType(typeof(ProductoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Validar(int id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ValidarAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Alta de producto. Precio mayor que cero y categoría existente y activa.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearProductoDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(dto, cancellationToken);
        return ApiResults.From(this, resultado, creado =>
            CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado));
    }

    /// <summary>Actualiza categoría, nombre, descripción y precio. El cliente no puede fijar el estado desde este contrato.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarProductoDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ActualizarAsync(id, dto, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Baja lógica. El producto deja de verse en el catálogo y no puede entrar en pedidos nuevos.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.DesactivarAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }
}
