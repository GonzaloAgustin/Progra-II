namespace Inventario.Api.Models;

public class InventarioItem
{
    public int ProductoId { get; set; }
    public int StockTotal { get; set; }
    public int StockReservado { get; set; }
    public byte[]? Version { get; set; }
}

public class Reserva
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public string Estado { get; set; } = EstadosReserva.Activa;
    public DateTime FechaCreacion { get; set; }
    public List<ReservaDetalle> Detalles { get; set; } = new();
}

public class ReservaDetalle
{
    public int Id { get; set; }
    public Guid ReservaId { get; set; }
    public Reserva Reserva { get; set; } = null!;
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

public static class EstadosReserva
{
    public const string Activa = "ACTIVA";
    public const string Liberada = "LIBERADA";
}
