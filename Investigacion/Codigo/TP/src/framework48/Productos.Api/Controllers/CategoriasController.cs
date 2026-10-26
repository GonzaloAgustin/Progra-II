using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Productos.Api.DTOs;
using Productos.Api.Infrastructure;
using Productos.Api.Services;

namespace Productos.Api.Controllers;

[RoutePrefix("api/categorias")]
public class CategoriasController : ApiController
{
    private readonly ICategoriaService _servicio;

    public CategoriasController(ICategoriaService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista las categorías activas.</summary>
    [HttpGet, Route("")]
    [ResponseType(typeof(IEnumerable<CategoriaDto>))]
    public async Task<IHttpActionResult> Listar(CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.ListarAsync(cancellationToken));
    }

    /// <summary>Obtiene una categoría activa. 404 si no existe o está inactiva.</summary>
    [HttpGet, Route("{id:int}")]
    [ResponseType(typeof(CategoriaDto))]
    public async Task<IHttpActionResult> Obtener(int id, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.ObtenerAsync(id, cancellationToken));
    }

    /// <summary>Crea una categoría. Nombre obligatorio, máximo 150, único entre activas sin distinguir mayúsculas. 201, 400 o 409.</summary>
    [HttpPost, Route("")]
    [ResponseType(typeof(CategoriaDto))]
    public async Task<IHttpActionResult> Crear([FromBody] CrearCategoriaDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(dto, cancellationToken);
        return ApiResults.Desde(this, resultado, creado => Created(new Uri(Request.RequestUri, creado.Id.ToString()), creado));
    }

    /// <summary>Actualiza nombre y descripción. No reactiva una categoría dada de baja.</summary>
    [HttpPut, Route("{id:int}")]
    [ResponseType(typeof(CategoriaDto))]
    public async Task<IHttpActionResult> Actualizar(int id, [FromBody] ActualizarCategoriaDto dto, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.ActualizarAsync(id, dto, cancellationToken));
    }

    /// <summary>Baja lógica. No borra la fila. Responde 204.</summary>
    [HttpDelete, Route("{id:int}")]
    public async Task<IHttpActionResult> Desactivar(int id, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.DesactivarAsync(id, cancellationToken));
    }
}
