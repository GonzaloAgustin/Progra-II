using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Inventario.Api.DTOs;
using Inventario.Api.Infrastructure;
using Inventario.Api.Services;

namespace Inventario.Api.Controllers;

[RoutePrefix("api/inventario")]
public class InventarioController : ApiController
{
    private readonly IInventarioService _servicio;

    public InventarioController(IInventarioService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Consulta stock total, reservado y disponible.</summary>
    [HttpGet, Route("{productoId:int}")]
    [ResponseType(typeof(StockDto))]
    public async Task<IHttpActionResult> Obtener(int productoId, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.ObtenerAsync(productoId, cancellationToken));
    }

    /// <summary>Fija el stock total. No lo incrementa. Crea la fila si el producto está activo. 409 si el total queda debajo de lo reservado. Ejemplo: {"stockTotal":10}.</summary>
    [HttpPut, Route("{productoId:int}")]
    [ResponseType(typeof(StockDto))]
    public async Task<IHttpActionResult> Establecer(int productoId, [FromBody] EstablecerStockDto dto, CancellationToken cancellationToken)
    {
        return ApiResults.Desde(this, await _servicio.EstablecerTotalAsync(productoId, dto, cancellationToken));
    }
}
