using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Productos.Api.Data;
using Productos.Api.DTOs;
using Productos.Api.Models;

namespace Productos.Api.Services;

public interface ICategoriaService
{
    Task<Resultado<List<CategoriaDto>>> ListarAsync(CancellationToken cancellationToken);
    Task<Resultado<CategoriaDto>> ObtenerAsync(int id, CancellationToken cancellationToken);
    Task<Resultado<CategoriaDto>> CrearAsync(CrearCategoriaDto dto, CancellationToken cancellationToken);
    Task<Resultado<CategoriaDto>> ActualizarAsync(int id, ActualizarCategoriaDto dto, CancellationToken cancellationToken);
    Task<Resultado<CategoriaDto>> DesactivarAsync(int id, CancellationToken cancellationToken);
}

public sealed class CategoriaService : ICategoriaService
{
    private readonly ProductosContext _db;

    public CategoriaService(ProductosContext db)
    {
        _db = db;
    }

    public async Task<Resultado<List<CategoriaDto>>> ListarAsync(CancellationToken cancellationToken)
    {
        var items = await _db.Categorias.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Id)
            .Select(c => new CategoriaDto { Id = c.Id, Nombre = c.Nombre, Descripcion = c.Descripcion, Activo = c.Activo })
            .ToListAsync(cancellationToken);
        return Resultado<List<CategoriaDto>>.Ok(items);
    }

    public async Task<Resultado<CategoriaDto>> ObtenerAsync(int id, CancellationToken cancellationToken)
    {
        var categoria = await _db.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.Activo, cancellationToken);
        return categoria == null
            ? Resultado<CategoriaDto>.NotFound("La categoría no existe o no está visible.")
            : Resultado<CategoriaDto>.Ok(Map(categoria));
    }

    public async Task<Resultado<CategoriaDto>> CrearAsync(CrearCategoriaDto dto, CancellationToken cancellationToken)
    {
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        var normalizado = Nombres.Normalizar(nombre);
        if (normalizado.Length == 0)
            return Resultado<CategoriaDto>.BadRequest("El nombre es obligatorio.");
        if (await ExisteActivaAsync(normalizado, null, cancellationToken))
            return Resultado<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");

        var categoria = new Categoria
        {
            Nombre = nombre,
            NombreNormalizado = normalizado,
            Descripcion = dto.Descripcion == null ? null : dto.Descripcion.Trim(),
            Activo = true
        };
        _db.Categorias.Add(categoria);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Resultado<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");
        }

        return Resultado<CategoriaDto>.Created(Map(categoria));
    }

    public async Task<Resultado<CategoriaDto>> ActualizarAsync(int id, ActualizarCategoriaDto dto, CancellationToken cancellationToken)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categoria == null)
            return Resultado<CategoriaDto>.NotFound("La categoría no existe.");

        var nombre = (dto.Nombre ?? string.Empty).Trim();
        var normalizado = Nombres.Normalizar(nombre);
        if (normalizado.Length == 0)
            return Resultado<CategoriaDto>.BadRequest("El nombre es obligatorio.");
        if (categoria.Activo && await ExisteActivaAsync(normalizado, id, cancellationToken))
            return Resultado<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");

        categoria.Nombre = nombre;
        categoria.NombreNormalizado = normalizado;
        categoria.Descripcion = dto.Descripcion == null ? null : dto.Descripcion.Trim();
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Resultado<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");
        }

        return Resultado<CategoriaDto>.Ok(Map(categoria));
    }

    public async Task<Resultado<CategoriaDto>> DesactivarAsync(int id, CancellationToken cancellationToken)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categoria == null)
            return Resultado<CategoriaDto>.NotFound("La categoría no existe.");
        categoria.Activo = false;
        await _db.SaveChangesAsync(cancellationToken);
        return Resultado<CategoriaDto>.NoContent();
    }

    private Task<bool> ExisteActivaAsync(string normalizado, int? exceptoId, CancellationToken cancellationToken)
    {
        return _db.Categorias.AnyAsync(c => c.Activo && c.NombreNormalizado == normalizado && (exceptoId == null || c.Id != exceptoId), cancellationToken);
    }

    private static CategoriaDto Map(Categoria categoria) => new CategoriaDto
    {
        Id = categoria.Id,
        Nombre = categoria.Nombre,
        Descripcion = categoria.Descripcion,
        Activo = categoria.Activo
    };
}
