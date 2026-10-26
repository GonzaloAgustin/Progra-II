using System.ComponentModel.DataAnnotations;

namespace Productos.Api.DTOs;

public sealed record CategoriaResumenDto(int Id, string Nombre);

public sealed record CategoriaDto(int Id, string Nombre, string? Descripcion, bool Activo);

public sealed record ProductoDto(
    int Id,
    int CategoriaId,
    string Nombre,
    string? Descripcion,
    decimal Precio,
    bool Activo,
    DateTime FechaCreacion,
    CategoriaResumenDto Categoria);

public sealed record ProductoValidacionDto(
    int Id,
    string Nombre,
    decimal Precio,
    bool Activo,
    int CategoriaId,
    bool CategoriaActiva);

public sealed class CrearCategoriaDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; set; }
}

public sealed class ActualizarCategoriaDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; set; }
}

public sealed class CrearProductoDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public int CategoriaId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; set; }

    public decimal Precio { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Precio <= 0)
            yield return new ValidationResult("El precio debe ser mayor que cero.", new[] { nameof(Precio) });
    }
}

public sealed class ActualizarProductoDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public int CategoriaId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; set; }

    public decimal Precio { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Precio <= 0)
            yield return new ValidationResult("El precio debe ser mayor que cero.", new[] { nameof(Precio) });
    }
}
