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

[RoutePrefix("api/productos")]
public class ProductosController : ApiController
{
    private readonly IProductoService _servicio;

    public ProductosController(IProductoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Catálogo público: productos activos de categorías activas. Filtra con categoriaId.</summary>
    [HttpGet, Route("")]
    [ResponseType(typeof(IEnumerable<ProductoDto>))]
    public async Task<IHttpActionResult> Listar(int? categoriaId = null, CancellationToken cancellationToken = default)
    {
        return ApiResults.Desde(this, await _servicio.ListarAsync(categoriaId, cancellationToken));
    }

    /// <summary>Obtiene un producto visible. 404 si no existe, está inactivo o su categoría está inactiva.</summary>
    [HttpGet, Route("{id:int}")]
    [ResponseType(typeof(ProductoDto))]
    public async Task<IHttpActionResult> Obtener(int id, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.ObtenerAsync(id, cancellationToken));
    }

    /// <summary>Contrato interno: precio, activo y categoriaActiva aunque el producto no esté en el catálogo. 404 solo si no existe.</summary>
    [HttpGet, Route("{id:int}/validacion")]
    [ResponseType(typeof(ProductoValidacionDto))]
    public async Task<IHttpActionResult> Validar(int id, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.ValidarAsync(id, cancellationToken));
    }

    /// <summary>Crea un producto. Precio mayor que cero y categoría activa. Ejemplo: {"categoriaId":1,"nombre":"Teclado","descripcion":"Mecánico","precio":15000}.</summary>
    [HttpPost, Route("")]
    [ResponseType(typeof(ProductoDto))]
    public async Task<IHttpActionResult> Crear([FromBody] CrearProductoDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(dto, cancellationToken);
        return ApiResults.Desde(this, resultado, creado => Created(new Uri(Request.RequestUri, creado.Id.ToString()), creado));
    }

    /// <summary>Actualiza nombre, descripción, precio y categoría. La categoría destino debe estar activa.</summary>
    [HttpPut, Route("{id:int}")]
    [ResponseType(typeof(ProductoDto))]
    public async Task<IHttpActionResult> Actualizar(int id, [FromBody] ActualizarProductoDto dto, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.ActualizarAsync(id, dto, cancellationToken));
    }

    /// <summary>Baja lógica. El producto deja de verse en el catálogo y no entra en pedidos nuevos. Responde 204.</summary>
    [HttpDelete, Route("{id:int}")]
    public async Task<IHttpActionResult> Desactivar(int id, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.DesactivarAsync(id, cancellationToken));
    }
}
