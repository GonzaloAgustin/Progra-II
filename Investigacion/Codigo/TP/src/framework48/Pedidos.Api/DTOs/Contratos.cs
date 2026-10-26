using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Pedidos.Api.DTOs;

public class ItemPedidoDto
{
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public class PedidoDto
{
    public Guid Id { get; set; }
    public string Estado { get; set; }
    public decimal Total { get; set; }
    public Guid? ReservaId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public List<ItemPedidoDto> Items { get; set; }
}

public class ReconciliacionDto
{
    public int PendientesRevisados { get; set; }
    public int Confirmados { get; set; }
    public int Rechazados { get; set; }
    public int CancelacionesRevisadas { get; set; }
    public int Cancelados { get; set; }
    public int SinCambios { get; set; }
}

public class CrearPedidoDto : IValidatableObject
{
    [Required]
    public List<ItemSolicitadoDto> Items { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Items == null || Items.Count == 0)
            yield return new ValidationResult("El pedido debe tener al menos un ítem.", new[] { nameof(Items) });
        else if (Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            yield return new ValidationResult("No se permiten productos repetidos en el pedido.", new[] { nameof(Items) });
    }
}

public class ItemSolicitadoDto
{
    [Range(1, int.MaxValue)]
    public int ProductoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser positiva.")]
    public int Cantidad { get; set; }
}
