using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inventario.Api.Clients;
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;

namespace Inventario.Api.Services;

public interface IInventarioService
{
    Task<Resultado<StockDto>> ObtenerAsync(int productoId, CancellationToken cancellationToken);
    Task<Resultado<StockDto>> EstablecerTotalAsync(int productoId, EstablecerStockDto dto, CancellationToken cancellationToken);
    Task<Resultado<ReservaDto>> ReservarAsync(CrearReservaDto dto, CancellationToken cancellationToken);
    Task<Resultado<ReservaDto>> LiberarAsync(Guid reservaId, CancellationToken cancellationToken);
}

public sealed class InventarioService : IInventarioService
{
    private readonly InventarioContext _db;
    private readonly IProductosCliente _productos;

    public InventarioService(InventarioContext db, IProductosCliente productos)
    {
        _db = db;
        _productos = productos;
    }

    public async Task<Resultado<StockDto>> ObtenerAsync(int productoId, CancellationToken cancellationToken)
    {
        var item = await _db.Inventarios.AsNoTracking().FirstOrDefaultAsync(i => i.ProductoId == productoId, cancellationToken);
        return item == null
            ? Resultado<StockDto>.NotFound("No hay inventario para el producto.")
            : Resultado<StockDto>.Ok(MapStock(item));
    }

    public async Task<Resultado<StockDto>> EstablecerTotalAsync(int productoId, EstablecerStockDto dto, CancellationToken cancellationToken)
    {
        if (dto.StockTotal < 0)
            return Resultado<StockDto>.BadRequest("El stock total no puede ser negativo.");

        var producto = await _productos.ObtenerAsync(productoId, cancellationToken);
        if (producto.Estado == ConsultaEstado.Timeout || producto.Estado == ConsultaEstado.NoDisponible)
            return Resultado<StockDto>.Unavailable("No se pudo validar el producto en Productos.");
        if (producto.Estado == ConsultaEstado.NoEncontrado)
            return Resultado<StockDto>.NotFound("El producto no existe.");
        if (!producto.Activo || !producto.CategoriaActiva)
            return Resultado<StockDto>.Conflict("El producto no está activo.");

        var item = await _db.Inventarios.FirstOrDefaultAsync(i => i.ProductoId == productoId, cancellationToken);
        if (item == null)
        {
            item = new InventarioItem { ProductoId = productoId, StockTotal = dto.StockTotal, StockReservado = 0 };
            _db.Inventarios.Add(item);
        }
        else
        {
            if (dto.StockTotal < item.StockReservado)
                return Resultado<StockDto>.Conflict("El stock total no puede ser menor que el stock reservado.");
            item.StockTotal = dto.StockTotal;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Resultado<StockDto>.Ok(MapStock(item));
    }

    public async Task<Resultado<ReservaDto>> ReservarAsync(CrearReservaDto dto, CancellationToken cancellationToken)
    {
        if (dto.PedidoId == Guid.Empty)
            return Resultado<ReservaDto>.BadRequest("El pedidoId es obligatorio.");
        if (dto.Items == null || dto.Items.Count == 0)
            return Resultado<ReservaDto>.BadRequest("La reserva debe incluir al menos un ítem.");
        if (dto.Items.Any(i => i.Cantidad <= 0 || i.ProductoId <= 0))
            return Resultado<ReservaDto>.BadRequest("Cada ítem debe tener producto y cantidad positivos.");
        if (dto.Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            return Resultado<ReservaDto>.BadRequest("No se permiten productos repetidos en la reserva.");

        for (var intento = 1; intento <= 3; intento++)
        {
            try
            {
                return await ReservarUnaVezAsync(dto, cancellationToken);
            }
            catch (Exception ex) when (intento < 3 && EsDeadlock(ex))
            {
                Trace.TraceWarning("Deadlock al reservar {0}. Reintento {1}.", dto.PedidoId, intento);
            }
        }

        throw new InvalidOperationException("No se pudo reservar después de varios reintentos.");
    }

    private async Task<Resultado<ReservaDto>> ReservarUnaVezAsync(CrearReservaDto dto, CancellationToken cancellationToken)
    {
        using (var transaccion = _db.Database.BeginTransaction(IsolationLevel.Serializable))
        {
            try
            {
                var existente = await _db.Reservas.Include(r => r.Detalles).FirstOrDefaultAsync(r => r.PedidoId == dto.PedidoId, cancellationToken);
                if (existente != null)
                {
                    transaccion.Commit();
                    if (!MismosItems(existente, dto))
                        return Resultado<ReservaDto>.Conflict("El pedidoId ya tiene una reserva con otro contenido.");
                    return Resultado<ReservaDto>.Ok(MapReserva(existente));
                }

                foreach (var item in dto.Items.OrderBy(i => i.ProductoId))
                {
                    var actualizados = _db.Database.ExecuteSqlCommand(
                        "UPDATE Inventarios SET StockReservado = StockReservado + @p0 WHERE ProductoId = @p1 AND StockReservado + @p0 <= StockTotal",
                        item.Cantidad,
                        item.ProductoId);
                    if (actualizados != 1)
                    {
                        transaccion.Rollback();
                        return Resultado<ReservaDto>.Conflict("Stock insuficiente para uno o más productos. No se reservó ningún ítem.");
                    }
                }

                var reserva = new Reserva
                {
                    Id = Guid.NewGuid(),
                    PedidoId = dto.PedidoId,
                    Estado = EstadosReserva.Activa,
                    FechaCreacion = DateTime.UtcNow,
                    Detalles = dto.Items.Select(i => new ReservaDetalle { ProductoId = i.ProductoId, Cantidad = i.Cantidad }).ToList()
                };
                _db.Reservas.Add(reserva);
                await _db.SaveChangesAsync(cancellationToken);
                transaccion.Commit();
                return Resultado<ReservaDto>.Created(MapReserva(reserva));
            }
            catch (DbUpdateException)
            {
                RollbackSeguro(transaccion);
                var again = await _db.Reservas.Include(r => r.Detalles).FirstOrDefaultAsync(r => r.PedidoId == dto.PedidoId, cancellationToken);
                if (again == null)
                    throw;
                if (!MismosItems(again, dto))
                    return Resultado<ReservaDto>.Conflict("El pedidoId ya tiene una reserva con otro contenido.");
                return Resultado<ReservaDto>.Ok(MapReserva(again));
            }
        }
    }

    public async Task<Resultado<ReservaDto>> LiberarAsync(Guid reservaId, CancellationToken cancellationToken)
    {
        using (var transaccion = _db.Database.BeginTransaction(IsolationLevel.Serializable))
        {
            var reserva = await _db.Reservas.Include(r => r.Detalles).FirstOrDefaultAsync(r => r.Id == reservaId, cancellationToken);
            if (reserva == null)
            {
                transaccion.Rollback();
                return Resultado<ReservaDto>.NotFound("La reserva no existe.");
            }

            if (reserva.Estado == EstadosReserva.Liberada)
            {
                transaccion.Commit();
                return Resultado<ReservaDto>.Ok(MapReserva(reserva));
            }

            foreach (var detalle in reserva.Detalles.OrderBy(d => d.ProductoId))
            {
                var actualizados = _db.Database.ExecuteSqlCommand(
                    "UPDATE Inventarios SET StockReservado = StockReservado - @p0 WHERE ProductoId = @p1 AND StockReservado >= @p0",
                    detalle.Cantidad,
                    detalle.ProductoId);
                if (actualizados != 1)
                {
                    transaccion.Rollback();
                    return Resultado<ReservaDto>.Conflict("No se pudo liberar la reserva sin dejar el stock en negativo.");
                }
            }

            reserva.Estado = EstadosReserva.Liberada;
            await _db.SaveChangesAsync(cancellationToken);
            transaccion.Commit();
            return Resultado<ReservaDto>.Ok(MapReserva(reserva));
        }
    }

    private static bool MismosItems(Reserva reserva, CrearReservaDto dto)
    {
        var actuales = reserva.Detalles.OrderBy(d => d.ProductoId).Select(d => d.ProductoId + ":" + d.Cantidad);
        var nuevos = dto.Items.OrderBy(i => i.ProductoId).Select(i => i.ProductoId + ":" + i.Cantidad);
        return actuales.SequenceEqual(nuevos);
    }

    private static StockDto MapStock(InventarioItem item) => new StockDto
    {
        ProductoId = item.ProductoId,
        StockTotal = item.StockTotal,
        StockReservado = item.StockReservado,
        StockDisponible = item.StockTotal - item.StockReservado
    };

    private static ReservaDto MapReserva(Reserva reserva) => new ReservaDto
    {
        Id = reserva.Id,
        PedidoId = reserva.PedidoId,
        Estado = reserva.Estado,
        FechaCreacion = reserva.FechaCreacion,
        Items = reserva.Detalles.OrderBy(d => d.ProductoId).Select(d => new ItemReservaDto { ProductoId = d.ProductoId, Cantidad = d.Cantidad }).ToList()
    };

    private static bool EsDeadlock(Exception ex)
    {
        for (var actual = ex; actual != null; actual = actual.InnerException)
        {
            if (actual.Message.IndexOf("deadlock", StringComparison.OrdinalIgnoreCase) >= 0
                || actual.Message.IndexOf("interbloqueo", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            var numero = actual.GetType().GetProperty("Number")?.GetValue(actual) as int?;
            if (numero == 1205)
                return true;
        }
        return false;
    }

    private static void RollbackSeguro(DbContextTransaction transaccion)
    {
        try { transaccion.Rollback(); }
        catch (InvalidOperationException) { }
    }
}
