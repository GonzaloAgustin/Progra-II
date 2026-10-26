using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Productos.Api.Data;
using Productos.Api.DTOs;
using Productos.Api.Models;

namespace Productos.Api.Services;

public interface IProductoService
{
    Task<Resultado<List<ProductoDto>>> ListarAsync(int? categoriaId, CancellationToken cancellationToken);
    Task<Resultado<ProductoDto>> ObtenerAsync(int id, CancellationToken cancellationToken);
    Task<Resultado<ProductoValidacionDto>> ValidarAsync(int id, CancellationToken cancellationToken);
    Task<Resultado<ProductoDto>> CrearAsync(CrearProductoDto dto, CancellationToken cancellationToken);
    Task<Resultado<ProductoDto>> ActualizarAsync(int id, ActualizarProductoDto dto, CancellationToken cancellationToken);
    Task<Resultado<ProductoDto>> DesactivarAsync(int id, CancellationToken cancellationToken);
}

public sealed class ProductoService : IProductoService
{
    private readonly ProductosContext _db;

    public ProductoService(ProductosContext db)
    {
        _db = db;
    }

    public async Task<Resultado<List<ProductoDto>>> ListarAsync(int? categoriaId, CancellationToken cancellationToken)
    {
        var consulta = Visible();
        if (categoriaId.HasValue)
            consulta = consulta.Where(p => p.CategoriaId == categoriaId.Value);
        var items = await consulta.OrderBy(p => p.Id).ToListAsync(cancellationToken);
        return Resultado<List<ProductoDto>>.Ok(items.Select(Map).ToList());
    }

    public async Task<Resultado<ProductoDto>> ObtenerAsync(int id, CancellationToken cancellationToken)
    {
        var producto = await Visible().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return producto == null
            ? Resultado<ProductoDto>.NotFound("El producto no existe o no está visible en el catálogo.")
            : Resultado<ProductoDto>.Ok(Map(producto));
    }

    public async Task<Resultado<ProductoValidacionDto>> ValidarAsync(int id, CancellationToken cancellationToken)
    {
        var producto = await _db.Productos.AsNoTracking().Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto == null)
            return Resultado<ProductoValidacionDto>.NotFound("El producto no existe.");
        return Resultado<ProductoValidacionDto>.Ok(new ProductoValidacionDto
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            Precio = producto.Precio,
            Activo = producto.Activo,
            CategoriaId = producto.CategoriaId,
            CategoriaActiva = producto.Categoria.Activo
        });
    }

    public async Task<Resultado<ProductoDto>> CrearAsync(CrearProductoDto dto, CancellationToken cancellationToken)
    {
        if (dto.Precio <= 0)
            return Resultado<ProductoDto>.BadRequest("El precio debe ser mayor que cero.");
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        if (nombre.Length == 0)
            return Resultado<ProductoDto>.BadRequest("El nombre es obligatorio.");

        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId, cancellationToken);
        if (categoria == null)
            return Resultado<ProductoDto>.BadRequest("La categoría no existe.");
        if (!categoria.Activo)
            return Resultado<ProductoDto>.BadRequest("La categoría no está activa.");

        var producto = new Producto
        {
            CategoriaId = categoria.Id,
            Categoria = categoria,
            Nombre = nombre,
            Descripcion = dto.Descripcion == null ? null : dto.Descripcion.Trim(),
            Precio = dto.Precio,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync(cancellationToken);
        return Resultado<ProductoDto>.Created(Map(producto));
    }

    public async Task<Resultado<ProductoDto>> ActualizarAsync(int id, ActualizarProductoDto dto, CancellationToken cancellationToken)
    {
        if (dto.Precio <= 0)
            return Resultado<ProductoDto>.BadRequest("El precio debe ser mayor que cero.");
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        if (nombre.Length == 0)
            return Resultado<ProductoDto>.BadRequest("El nombre es obligatorio.");

        var producto = await _db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto == null)
            return Resultado<ProductoDto>.NotFound("El producto no existe.");
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == dto.CategoriaId, cancellationToken);
        if (categoria == null)
            return Resultado<ProductoDto>.BadRequest("La categoría no existe.");
        if (!categoria.Activo)
            return Resultado<ProductoDto>.BadRequest("La categoría no está activa.");

        producto.CategoriaId = categoria.Id;
        producto.Categoria = categoria;
        producto.Nombre = nombre;
        producto.Descripcion = dto.Descripcion == null ? null : dto.Descripcion.Trim();
        producto.Precio = dto.Precio;
        await _db.SaveChangesAsync(cancellationToken);
        return Resultado<ProductoDto>.Ok(Map(producto));
    }

    public async Task<Resultado<ProductoDto>> DesactivarAsync(int id, CancellationToken cancellationToken)
    {
        var producto = await _db.Productos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto == null)
            return Resultado<ProductoDto>.NotFound("El producto no existe.");
        producto.Activo = false;
        await _db.SaveChangesAsync(cancellationToken);
        return Resultado<ProductoDto>.NoContent();
    }

    private IQueryable<Producto> Visible() =>
        _db.Productos.AsNoTracking().Include(p => p.Categoria).Where(p => p.Activo && p.Categoria.Activo);

    private static ProductoDto Map(Producto producto) => new ProductoDto
    {
        Id = producto.Id,
        CategoriaId = producto.CategoriaId,
        Nombre = producto.Nombre,
        Descripcion = producto.Descripcion,
        Precio = producto.Precio,
        Activo = producto.Activo,
        FechaCreacion = producto.FechaCreacion,
        Categoria = new CategoriaResumenDto { Id = producto.Categoria.Id, Nombre = producto.Categoria.Nombre }
    };
}
