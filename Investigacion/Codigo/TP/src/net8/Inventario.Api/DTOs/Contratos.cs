using System.ComponentModel.DataAnnotations;

namespace Inventario.Api.DTOs;

public sealed record StockDto(int ProductoId, int StockTotal, int StockReservado, int StockDisponible);

public sealed record ItemReservaDto(int ProductoId, int Cantidad);

public sealed record ReservaDto(Guid Id, Guid PedidoId, string Estado, DateTime FechaCreacion, IReadOnlyList<ItemReservaDto> Items);

public sealed class EstablecerStockDto
{
    [Range(0, int.MaxValue, ErrorMessage = "El stock total no puede ser negativo.")]
    public int StockTotal { get; set; }
}

public sealed class CrearReservaDto : IValidatableObject
{
    [Required]
    public Guid PedidoId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "La reserva debe incluir al menos un ítem.")]
    public List<ItemCantidadDto> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PedidoId == Guid.Empty)
            yield return new ValidationResult("El pedidoId es obligatorio.", new[] { nameof(PedidoId) });

        if (Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            yield return new ValidationResult("No se permiten productos repetidos en la reserva.", new[] { nameof(Items) });
    }
}

public sealed class ItemCantidadDto
{
    [Range(1, int.MaxValue)]
    public int ProductoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser positiva.")]
    public int Cantidad { get; set; }
}
