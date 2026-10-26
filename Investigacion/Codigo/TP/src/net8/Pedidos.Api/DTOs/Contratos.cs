using System.ComponentModel.DataAnnotations;

namespace Pedidos.Api.DTOs;

public sealed record ItemPedidoDto(int ProductoId, int Cantidad, decimal PrecioUnitario, decimal Subtotal);

public sealed record PedidoDto(
    Guid Id,
    string Estado,
    decimal Total,
    Guid? ReservaId,
    DateTime FechaCreacion,
    IReadOnlyList<ItemPedidoDto> Items);

public sealed record ReconciliacionDto(
    int PendientesRevisados,
    int Confirmados,
    int Rechazados,
    int CancelacionesRevisadas,
    int Cancelados,
    int SinCambios);

public sealed class CrearPedidoDto : IValidatableObject
{
    [Required]
    [MinLength(1, ErrorMessage = "El pedido debe tener al menos un ítem.")]
    public List<ItemSolicitadoDto> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            yield return new ValidationResult("No se permiten productos repetidos en el pedido.", new[] { nameof(Items) });
    }
}

public sealed class ItemSolicitadoDto
{
    [Range(1, int.MaxValue)]
    public int ProductoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser positiva.")]
    public int Cantidad { get; set; }
}
