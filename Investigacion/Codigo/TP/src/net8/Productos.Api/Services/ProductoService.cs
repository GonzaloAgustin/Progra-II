using Microsoft.EntityFrameworkCore;
using Productos.Api.Data;
using Productos.Api.DTOs;
using Productos.Api.Models;

namespace Productos.Api.Services;

public interface IProductoService
{
    Task<ServiceResult<IReadOnlyList<ProductoDto>>> ListarAsync(int? categoriaId, CancellationToken cancellationToken);
    Task<ServiceResult<ProductoDto>> ObtenerAsync(int id, CancellationToken cancellationToken);
    Task<ServiceResult<ProductoValidacionDto>> ValidarAsync(int id, CancellationToken cancellationToken);
    Task<ServiceResult<ProductoDto>> CrearAsync(CrearProductoDto dto, CancellationToken cancellationToken);
    Task<ServiceResult<ProductoDto>> ActualizarAsync(int id, ActualizarProductoDto dto, CancellationToken cancellationToken);
    Task<ServiceResult<ProductoDto>> DesactivarAsync(int id, CancellationToken cancellationToken);
}

public sealed class ProductoService : IProductoService
{
    private readonly ProductosDbContext _db;

    public ProductoService(ProductosDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<IReadOnlyList<ProductoDto>>> ListarAsync(int? categoriaId, CancellationToken cancellationToken)
    {
        var consulta = CatalogoVisible();
        if (categoriaId.HasValue)
            consulta = consulta.Where(p => p.CategoriaId == categoriaId.Value);

        var items = await consulta.OrderBy(p => p.Id).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<ProductoDto>>.Ok(items.Select(Map).ToList());
    }

    public async Task<ServiceResult<ProductoDto>> ObtenerAsync(int id, CancellationToken cancellationToken)
    {
        var producto = await CatalogoVisible().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return producto is null
            ? ServiceResult<ProductoDto>.NotFound("El producto no existe o no está visible en el catálogo.")
            : ServiceResult<ProductoDto>.Ok(Map(producto));
    }

    public async Task<ServiceResult<ProductoValidacionDto>> ValidarAsync(int id, CancellationToken cancellationToken)
    {
        var producto = await _db.Productos.AsNoTracking()
            .Include(p => p.Categoria)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (producto is null)
            return ServiceResult<ProductoValidacionDto>.NotFound("El producto no existe.");

        return ServiceResult<ProductoValidacionDto>.Ok(new ProductoValidacionDto(
            producto.Id,
            producto.Nombre,
            producto.Precio,
            producto.Activo,
            producto.CategoriaId,
            producto.Categoria.Activo));
    }

    public async Task<ServiceResult<ProductoDto>> CrearAsync(CrearProductoDto dto, CancellationToken cancellationToken)
    {
        if (dto.Precio <= 0)
            return ServiceResult<ProductoDto>.BadRequest("El precio debe ser mayor que cero.");

        var nombre = dto.Nombre.Trim();
        if (nombre.Length == 0)
            return ServiceResult<ProductoDto>.BadRequest("El nombre es obligatorio.");

        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId, cancellationToken);
        if (categoria is null)
            return ServiceResult<ProductoDto>.BadRequest("La categoría no existe.");
        if (!categoria.Activo)
            return ServiceResult<ProductoDto>.BadRequest("La categoría no está activa.");

        var producto = new Producto
        {
            CategoriaId = categoria.Id,
            Nombre = nombre,
            Descripcion = dto.Descripcion?.Trim(),
            Precio = dto.Precio,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
            Categoria = categoria
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<ProductoDto>.Created(Map(producto));
    }

    public async Task<ServiceResult<ProductoDto>> ActualizarAsync(int id, ActualizarProductoDto dto, CancellationToken cancellationToken)
    {
        if (dto.Precio <= 0)
            return ServiceResult<ProductoDto>.BadRequest("El precio debe ser mayor que cero.");

        var nombre = dto.Nombre.Trim();
        if (nombre.Length == 0)
            return ServiceResult<ProductoDto>.BadRequest("El nombre es obligatorio.");

        var producto = await _db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto is null)
            return ServiceResult<ProductoDto>.NotFound("El producto no existe.");

        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId, cancellationToken);
        if (categoria is null)
            return ServiceResult<ProductoDto>.BadRequest("La categoría no existe.");
        if (!categoria.Activo)
            return ServiceResult<ProductoDto>.BadRequest("La categoría no está activa.");

        producto.CategoriaId = categoria.Id;
        producto.Categoria = categoria;
        producto.Nombre = nombre;
        producto.Descripcion = dto.Descripcion?.Trim();
        producto.Precio = dto.Precio;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<ProductoDto>.Ok(Map(producto));
    }

    public async Task<ServiceResult<ProductoDto>> DesactivarAsync(int id, CancellationToken cancellationToken)
    {
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto is null)
            return ServiceResult<ProductoDto>.NotFound("El producto no existe.");

        producto.Activo = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<ProductoDto>.NoContent();
    }

    private IQueryable<Producto> CatalogoVisible() =>
        _db.Productos.AsNoTracking().Include(p => p.Categoria).Where(p => p.Activo && p.Categoria.Activo);

    private static ProductoDto Map(Producto producto) => new(
        producto.Id,
        producto.CategoriaId,
        producto.Nombre,
        producto.Descripcion,
        producto.Precio,
        producto.Activo,
        producto.FechaCreacion,
        new CategoriaResumenDto(producto.Categoria.Id, producto.Categoria.Nombre));
}
