using Microsoft.EntityFrameworkCore;
using Productos.Api.Data;
using Productos.Api.DTOs;
using Productos.Api.Models;

namespace Productos.Api.Services;

public interface ICategoriaService
{
    Task<ServiceResult<IReadOnlyList<CategoriaDto>>> ListarAsync(CancellationToken cancellationToken);
    Task<ServiceResult<CategoriaDto>> ObtenerAsync(int id, CancellationToken cancellationToken);
    Task<ServiceResult<CategoriaDto>> CrearAsync(CrearCategoriaDto dto, CancellationToken cancellationToken);
    Task<ServiceResult<CategoriaDto>> ActualizarAsync(int id, ActualizarCategoriaDto dto, CancellationToken cancellationToken);
    Task<ServiceResult<CategoriaDto>> DesactivarAsync(int id, CancellationToken cancellationToken);
}

public sealed class CategoriaService : ICategoriaService
{
    private readonly ProductosDbContext _db;

    public CategoriaService(ProductosDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<IReadOnlyList<CategoriaDto>>> ListarAsync(CancellationToken cancellationToken)
    {
        var items = await _db.Categorias.AsNoTracking()
            .Where(c => c.Activo)
            .OrderBy(c => c.Id)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Activo))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<CategoriaDto>>.Ok(items);
    }

    public async Task<ServiceResult<CategoriaDto>> ObtenerAsync(int id, CancellationToken cancellationToken)
    {
        var categoria = await _db.Categorias.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.Activo, cancellationToken);

        return categoria is null
            ? ServiceResult<CategoriaDto>.NotFound("La categoría no existe o no está visible.")
            : ServiceResult<CategoriaDto>.Ok(Map(categoria));
    }

    public async Task<ServiceResult<CategoriaDto>> CrearAsync(CrearCategoriaDto dto, CancellationToken cancellationToken)
    {
        var nombre = dto.Nombre.Trim();
        var normalizado = Normalizar(nombre);
        if (normalizado.Length == 0)
            return ServiceResult<CategoriaDto>.BadRequest("El nombre es obligatorio.");

        if (await ExisteActivaAsync(normalizado, null, cancellationToken))
            return ServiceResult<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");

        var categoria = new Categoria
        {
            Nombre = nombre,
            NombreNormalizado = normalizado,
            Descripcion = dto.Descripcion?.Trim(),
            Activo = true
        };
        _db.Categorias.Add(categoria);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");
        }

        return ServiceResult<CategoriaDto>.Created(Map(categoria));
    }

    public async Task<ServiceResult<CategoriaDto>> ActualizarAsync(int id, ActualizarCategoriaDto dto, CancellationToken cancellationToken)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categoria is null)
            return ServiceResult<CategoriaDto>.NotFound("La categoría no existe.");

        var nombre = dto.Nombre.Trim();
        var normalizado = Normalizar(nombre);
        if (normalizado.Length == 0)
            return ServiceResult<CategoriaDto>.BadRequest("El nombre es obligatorio.");

        if (categoria.Activo && await ExisteActivaAsync(normalizado, id, cancellationToken))
            return ServiceResult<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");

        categoria.Nombre = nombre;
        categoria.NombreNormalizado = normalizado;
        categoria.Descripcion = dto.Descripcion?.Trim();

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<CategoriaDto>.Conflict("Ya existe una categoría activa con ese nombre.");
        }

        return ServiceResult<CategoriaDto>.Ok(Map(categoria));
    }

    public async Task<ServiceResult<CategoriaDto>> DesactivarAsync(int id, CancellationToken cancellationToken)
    {
        var categoria = await _db.Categorias.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (categoria is null)
            return ServiceResult<CategoriaDto>.NotFound("La categoría no existe.");

        categoria.Activo = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<CategoriaDto>.NoContent();
    }

    private Task<bool> ExisteActivaAsync(string normalizado, int? exceptoId, CancellationToken cancellationToken)
    {
        return _db.Categorias.AnyAsync(
            c => c.Activo && c.NombreNormalizado == normalizado && (exceptoId == null || c.Id != exceptoId),
            cancellationToken);
    }

    internal static string Normalizar(string nombre) => nombre.Trim().ToUpperInvariant();

    private static CategoriaDto Map(Categoria categoria) =>
        new(categoria.Id, categoria.Nombre, categoria.Descripcion, categoria.Activo);
}
