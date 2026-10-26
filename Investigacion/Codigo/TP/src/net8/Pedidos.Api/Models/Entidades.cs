namespace Pedidos.Api.Models;

public class Pedido
{
    public Guid Id { get; set; }
    public string Estado { get; set; } = EstadosPedido.Pendiente;
    public decimal Total { get; set; }
    public Guid? ReservaId { get; set; }
    public DateTime FechaCreacion { get; set; }
    public List<PedidoDetalle> Detalles { get; set; } = new();
}

public class PedidoDetalle
{
    public int Id { get; set; }
    public Guid PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }
}

public static class EstadosPedido
{
    public const string Pendiente = "PENDIENTE";
    public const string Confirmado = "CONFIRMADO";
    public const string Rechazado = "RECHAZADO";
    public const string CancelacionPendiente = "CANCELACION_PENDIENTE";
    public const string Cancelado = "CANCELADO";
}
