using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Inventario.Api.DTOs;

public class StockDto
{
    public int ProductoId { get; set; }
    public int StockTotal { get; set; }
    public int StockReservado { get; set; }
    public int StockDisponible { get; set; }
}

public class ItemReservaDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

public class ReservaDto
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public string Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public List<ItemReservaDto> Items { get; set; }
}

public class EstablecerStockDto
{
    [Range(0, int.MaxValue, ErrorMessage = "El stock total no puede ser negativo.")]
    public int StockTotal { get; set; }
}

public class CrearReservaDto : IValidatableObject
{
    [Required]
    public Guid PedidoId { get; set; }

    [Required]
    public List<ItemCantidadDto> Items { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PedidoId == Guid.Empty)
            yield return new ValidationResult("El pedidoId es obligatorio.", new[] { nameof(PedidoId) });
        if (Items == null || Items.Count == 0)
            yield return new ValidationResult("La reserva debe incluir al menos un ítem.", new[] { nameof(Items) });
        else if (Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            yield return new ValidationResult("No se permiten productos repetidos en la reserva.", new[] { nameof(Items) });
    }
}

public class ItemCantidadDto
{
    [Range(1, int.MaxValue)]
    public int ProductoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser positiva.")]
    public int Cantidad { get; set; }
}
