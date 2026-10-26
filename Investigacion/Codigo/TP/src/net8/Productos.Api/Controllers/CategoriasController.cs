using Microsoft.AspNetCore.Mvc;
using Productos.Api.DTOs;
using Productos.Api.Infrastructure;
using Productos.Api.Services;

namespace Productos.Api.Controllers;

[ApiController]
[Route("api/categorias")]
public class CategoriasController : ControllerBase
{
    private readonly ICategoriaService _servicio;

    public CategoriasController(ICategoriaService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista las categorías activas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoriaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ListarAsync(cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Obtiene una categoría activa. Responde 404 si no existe o está inactiva.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ObtenerAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Crea una categoría. El nombre es obligatorio y único entre las activas, sin distinguir mayúsculas.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] CrearCategoriaDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(dto, cancellationToken);
        return ApiResults.From(this, resultado, creado =>
            CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado));
    }

    /// <summary>Actualiza nombre y descripción. No reactiva una categoría dada de baja.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarCategoriaDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ActualizarAsync(id, dto, cancellationToken);
        return ApiResults.From(this, resultado);
    }

    /// <summary>Baja lógica. No borra la fila ni sus productos. Responde 204.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.DesactivarAsync(id, cancellationToken);
        return ApiResults.From(this, resultado);
    }
}
