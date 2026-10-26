using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pedidos.Api.Clients;
using Pedidos.Api.Data;
using Pedidos.Api.DTOs;
using Pedidos.Api.Models;

namespace Pedidos.Api.Services;

public interface IPedidoService
{
    Task<Resultado<List<PedidoDto>>> ListarAsync(CancellationToken cancellationToken);
    Task<Resultado<PedidoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);
    Task<Resultado<PedidoDto>> CrearAsync(CrearPedidoDto dto, CancellationToken cancellationToken);
    Task<Resultado<PedidoDto>> CancelarAsync(Guid id, CancellationToken cancellationToken);
    Task<ReconciliacionDto> ReconciliarAsync(CancellationToken cancellationToken);
}

public sealed class PedidoService : IPedidoService
{
    private readonly PedidosContext _db;
    private readonly IProductosCliente _productos;
    private readonly IInventarioCliente _inventario;

    public PedidoService(PedidosContext db, IProductosCliente productos, IInventarioCliente inventario)
    {
        _db = db;
        _productos = productos;
        _inventario = inventario;
    }

    public async Task<Resultado<List<PedidoDto>>> ListarAsync(CancellationToken cancellationToken)
    {
        var pedidos = await _db.Pedidos.AsNoTracking().Include(p => p.Detalles).OrderByDescending(p => p.FechaCreacion).ToListAsync(cancellationToken);
        return Resultado<List<PedidoDto>>.Ok(pedidos.Select(Map).ToList());
    }

    public async Task<Resultado<PedidoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await _db.Pedidos.AsNoTracking().Include(p => p.Detalles).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return pedido == null ? Resultado<PedidoDto>.NotFound("El pedido no existe.") : Resultado<PedidoDto>.Ok(Map(pedido));
    }

    public async Task<Resultado<PedidoDto>> CrearAsync(CrearPedidoDto dto, CancellationToken cancellationToken)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return Resultado<PedidoDto>.BadRequest("El pedido debe tener al menos un ítem.");
        if (dto.Items.Any(i => i.Cantidad <= 0 || i.ProductoId <= 0))
            return Resultado<PedidoDto>.BadRequest("Cada ítem debe tener producto y cantidad positivos.");
        if (dto.Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            return Resultado<PedidoDto>.BadRequest("No se permiten productos repetidos en el pedido.");

        var detalles = new List<PedidoDetalle>();
        foreach (var item in dto.Items)
        {
            var producto = await _productos.ObtenerAsync(item.ProductoId, cancellationToken);
            if (producto.Estado == ConsultaEstado.Timeout)
                return Resultado<PedidoDto>.Unavailable("Productos no respondió a tiempo. No se creó el pedido.");
            if (producto.Estado == ConsultaEstado.NoDisponible)
                return Resultado<PedidoDto>.Unavailable("Productos no está disponible. No se creó el pedido.");
            if (producto.Estado == ConsultaEstado.NoEncontrado)
                return Resultado<PedidoDto>.BadRequest("El producto " + item.ProductoId + " no existe.");
            if (!producto.Activo || !producto.CategoriaActiva)
                return Resultado<PedidoDto>.BadRequest("El producto " + item.ProductoId + " no está disponible para la venta.");

            detalles.Add(new PedidoDetalle
            {
                ProductoId = item.ProductoId,
                Cantidad = item.Cantidad,
                PrecioUnitario = producto.Precio,
                Subtotal = CalculoPedido.Subtotal(item.Cantidad, producto.Precio)
            });
        }

        var pedido = new Pedido
        {
            Id = Guid.NewGuid(),
            Estado = EstadosPedido.Pendiente,
            Total = CalculoPedido.Total(detalles.Select(d => d.Subtotal)),
            FechaCreacion = DateTime.UtcNow,
            Detalles = detalles
        };
        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync(cancellationToken);
        Trace.TraceInformation("Pedido {0} persistido en PENDIENTE.", pedido.Id);
        return await IntentarReservarAsync(pedido, cancellationToken);
    }

    public async Task<Resultado<PedidoDto>> CancelarAsync(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await _db.Pedidos.Include(p => p.Detalles).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (pedido == null)
            return Resultado<PedidoDto>.NotFound("El pedido no existe.");
        if (pedido.Estado == EstadosPedido.Cancelado)
            return Resultado<PedidoDto>.Ok(Map(pedido));
        if (pedido.Estado == EstadosPedido.CancelacionPendiente)
            return await CompletarCancelacionAsync(pedido, cancellationToken);
        if (pedido.Estado != EstadosPedido.Confirmado)
            return Resultado<PedidoDto>.Conflict("Solo se pueden cancelar pedidos confirmados.");
        if (pedido.ReservaId == null)
            return Resultado<PedidoDto>.Conflict("El pedido confirmado no tiene una reserva asociada.");

        pedido.Estado = EstadosPedido.CancelacionPendiente;
        await _db.SaveChangesAsync(cancellationToken);
        return await CompletarCancelacionAsync(pedido, cancellationToken);
    }

    public async Task<ReconciliacionDto> ReconciliarAsync(CancellationToken cancellationToken)
    {
        var resumen = new ReconciliacionDto();
        var pendientes = await _db.Pedidos.Include(p => p.Detalles).Where(p => p.Estado == EstadosPedido.Pendiente).ToListAsync(cancellationToken);
        resumen.PendientesRevisados = pendientes.Count;
        foreach (var pedido in pendientes)
        {
            await IntentarReservarAsync(pedido, cancellationToken);
            if (pedido.Estado == EstadosPedido.Confirmado) resumen.Confirmados++;
            else if (pedido.Estado == EstadosPedido.Rechazado) resumen.Rechazados++;
            else resumen.SinCambios++;
        }

        var cancelaciones = await _db.Pedidos.Include(p => p.Detalles).Where(p => p.Estado == EstadosPedido.CancelacionPendiente).ToListAsync(cancellationToken);
        resumen.CancelacionesRevisadas = cancelaciones.Count;
        foreach (var pedido in cancelaciones)
        {
            await CompletarCancelacionAsync(pedido, cancellationToken);
            if (pedido.Estado == EstadosPedido.Cancelado) resumen.Cancelados++;
            else resumen.SinCambios++;
        }
        return resumen;
    }

    private async Task<Resultado<PedidoDto>> IntentarReservarAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        var items = pedido.Detalles.Select(d => Tuple.Create(d.ProductoId, d.Cantidad)).ToList();
        var reserva = await _inventario.ReservarAsync(pedido.Id, items, cancellationToken);
        if (reserva.Estado == ResultadoRemoto.Confirmada)
        {
            pedido.Estado = EstadosPedido.Confirmado;
            pedido.ReservaId = reserva.ReservaId;
            await _db.SaveChangesAsync(cancellationToken);
            return Resultado<PedidoDto>.Created(Map(pedido));
        }
        if (reserva.Estado == ResultadoRemoto.StockInsuficiente)
        {
            pedido.Estado = EstadosPedido.Rechazado;
            await _db.SaveChangesAsync(cancellationToken);
            return Resultado<PedidoDto>.Conflict("Stock insuficiente. El pedido fue rechazado y no quedó reserva parcial.").Con(pedido.Id, pedido.Estado);
        }
        return Resultado<PedidoDto>.Unavailable("Inventario no confirmó la reserva. El pedido quedó pendiente de reconciliación.").Con(pedido.Id, pedido.Estado);
    }

    private async Task<Resultado<PedidoDto>> CompletarCancelacionAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        if (pedido.ReservaId == null)
            return Resultado<PedidoDto>.Conflict("No hay reserva para liberar.").Con(pedido.Id, pedido.Estado);
        var liberacion = await _inventario.LiberarAsync(pedido.ReservaId.Value, cancellationToken);
        if (liberacion.Estado == ResultadoRemoto.Confirmada)
        {
            pedido.Estado = EstadosPedido.Cancelado;
            await _db.SaveChangesAsync(cancellationToken);
            return Resultado<PedidoDto>.Ok(Map(pedido));
        }
        if (liberacion.Estado == ResultadoRemoto.NoEncontrada)
            return Resultado<PedidoDto>.Conflict("La reserva asociada no existe. La cancelación sigue pendiente.").Con(pedido.Id, pedido.Estado);
        return Resultado<PedidoDto>.Unavailable("No se confirmó la liberación. El pedido sigue en cancelación pendiente.").Con(pedido.Id, pedido.Estado);
    }

    private static PedidoDto Map(Pedido pedido) => new PedidoDto
    {
        Id = pedido.Id,
        Estado = pedido.Estado,
        Total = pedido.Total,
        ReservaId = pedido.ReservaId,
        FechaCreacion = pedido.FechaCreacion,
        Items = pedido.Detalles.OrderBy(d => d.ProductoId).Select(d => new ItemPedidoDto
        {
            ProductoId = d.ProductoId,
            Cantidad = d.Cantidad,
            PrecioUnitario = d.PrecioUnitario,
            Subtotal = d.Subtotal
        }).ToList()
    };
}
