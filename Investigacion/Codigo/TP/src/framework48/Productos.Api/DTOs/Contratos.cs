using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Productos.Api.DTOs;

public class CategoriaResumenDto
{
    public int Id { get; set; }
    public string Nombre { get; set; }
}

public class CategoriaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public string Descripcion { get; set; }
    public bool Activo { get; set; }
}

public class ProductoDto
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Nombre { get; set; }
    public string Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public CategoriaResumenDto Categoria { get; set; }
}

public class ProductoValidacionDto
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public decimal Precio { get; set; }
    public bool Activo { get; set; }
    public int CategoriaId { get; set; }
    public bool CategoriaActiva { get; set; }
}

public class CrearCategoriaDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; }

    [MaxLength(500)]
    public string Descripcion { get; set; }
}

public class ActualizarCategoriaDto
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; }

    [MaxLength(500)]
    public string Descripcion { get; set; }
}

public class CrearProductoDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public int CategoriaId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; }

    [MaxLength(500)]
    public string Descripcion { get; set; }

    public decimal Precio { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Precio <= 0)
            yield return new ValidationResult("El precio debe ser mayor que cero.", new[] { nameof(Precio) });
    }
}

public class ActualizarProductoDto : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "La categoría es obligatoria.")]
    public int CategoriaId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(150)]
    public string Nombre { get; set; }

    [MaxLength(500)]
    public string Descripcion { get; set; }

    public decimal Precio { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Precio <= 0)
            yield return new ValidationResult("El precio debe ser mayor que cero.", new[] { nameof(Precio) });
    }
}
