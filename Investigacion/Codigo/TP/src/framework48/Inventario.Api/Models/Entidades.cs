using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventario.Api.Models;

public class InventarioItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int ProductoId { get; set; }
    public int StockTotal { get; set; }
    public int StockReservado { get; set; }
    [Timestamp]
    public byte[] Version { get; set; }
}

public class Reserva
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public string Estado { get; set; }
    public DateTime FechaCreacion { get; set; }
    public virtual ICollection<ReservaDetalle> Detalles { get; set; } = new List<ReservaDetalle>();
}

public class ReservaDetalle
{
    public int Id { get; set; }
    public Guid ReservaId { get; set; }
    public virtual Reserva Reserva { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
}

public static class EstadosReserva
{
    public const string Activa = "ACTIVA";
    public const string Liberada = "LIBERADA";
}
