using System.Data;
using Inventario.Api.Clients;
using Inventario.Api.Data;
using Inventario.Api.DTOs;
using Inventario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Services;

public interface IInventarioService
{
    Task<ServiceResult<StockDto>> ObtenerAsync(int productoId, CancellationToken cancellationToken);
    Task<ServiceResult<StockDto>> EstablecerTotalAsync(int productoId, EstablecerStockDto dto, CancellationToken cancellationToken);
    Task<ServiceResult<ReservaDto>> ReservarAsync(CrearReservaDto dto, CancellationToken cancellationToken);
    Task<ServiceResult<ReservaDto>> LiberarAsync(Guid reservaId, CancellationToken cancellationToken);
}

public sealed class InventarioService : IInventarioService
{
    private readonly InventarioDbContext _db;
    private readonly IProductosClient _productos;
    private readonly ILogger<InventarioService> _logger;

    public InventarioService(InventarioDbContext db, IProductosClient productos, ILogger<InventarioService> logger)
    {
        _db = db;
        _productos = productos;
        _logger = logger;
    }

    public async Task<ServiceResult<StockDto>> ObtenerAsync(int productoId, CancellationToken cancellationToken)
    {
        var item = await _db.Inventarios.AsNoTracking().FirstOrDefaultAsync(i => i.ProductoId == productoId, cancellationToken);
        return item is null
            ? ServiceResult<StockDto>.NotFound("No hay inventario para el producto.")
            : ServiceResult<StockDto>.Ok(MapStock(item));
    }

    public async Task<ServiceResult<StockDto>> EstablecerTotalAsync(int productoId, EstablecerStockDto dto, CancellationToken cancellationToken)
    {
        if (dto.StockTotal < 0)
            return ServiceResult<StockDto>.BadRequest("El stock total no puede ser negativo.");

        var producto = await _productos.ObtenerAsync(productoId, cancellationToken);
        if (producto.Estado is ConsultaEstado.Timeout or ConsultaEstado.NoDisponible)
            return ServiceResult<StockDto>.Unavailable("No se pudo validar el producto en Productos.");
        if (producto.Estado == ConsultaEstado.NoEncontrado)
            return ServiceResult<StockDto>.NotFound("El producto no existe.");
        if (!producto.Activo || !producto.CategoriaActiva)
            return ServiceResult<StockDto>.Conflict("El producto no está activo.");

        var item = await _db.Inventarios.FirstOrDefaultAsync(i => i.ProductoId == productoId, cancellationToken);
        if (item is null)
        {
            item = new InventarioItem
            {
                ProductoId = productoId,
                StockTotal = dto.StockTotal,
                StockReservado = 0
            };
            _db.Inventarios.Add(item);
        }
        else
        {
            if (dto.StockTotal < item.StockReservado)
                return ServiceResult<StockDto>.Conflict("El stock total no puede ser menor que el stock reservado.");
            item.StockTotal = dto.StockTotal;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<StockDto>.Ok(MapStock(item));
    }

    public async Task<ServiceResult<ReservaDto>> ReservarAsync(CrearReservaDto dto, CancellationToken cancellationToken)
    {
        if (dto.PedidoId == Guid.Empty)
            return ServiceResult<ReservaDto>.BadRequest("El pedidoId es obligatorio.");
        if (dto.Items is null || dto.Items.Count == 0)
            return ServiceResult<ReservaDto>.BadRequest("La reserva debe incluir al menos un ítem.");
        if (dto.Items.Any(i => i.Cantidad <= 0 || i.ProductoId <= 0))
            return ServiceResult<ReservaDto>.BadRequest("Cada ítem debe tener producto y cantidad positivos.");
        if (dto.Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            return ServiceResult<ReservaDto>.BadRequest("No se permiten productos repetidos en la reserva.");

        for (var intento = 1; intento <= 3; intento++)
        {
            try
            {
                return await ReservarUnaVezAsync(dto, cancellationToken);
            }
            catch (Exception ex) when (intento < 3 && EsDeadlock(ex))
            {
                _logger.LogWarning("Deadlock al reservar el pedido {PedidoId}. Reintento {Intento}.", dto.PedidoId, intento);
                _db.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("No se pudo reservar después de varios reintentos.");
    }

    private async Task<ServiceResult<ReservaDto>> ReservarUnaVezAsync(CrearReservaDto dto, CancellationToken cancellationToken)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var existente = await _db.Reservas.Include(r => r.Detalles)
                .FirstOrDefaultAsync(r => r.PedidoId == dto.PedidoId, cancellationToken);

            if (existente is not null)
            {
                await transaccion.CommitAsync(cancellationToken);
                if (!MismosItems(existente, dto))
                    return ServiceResult<ReservaDto>.Conflict("El pedidoId ya tiene una reserva con otro contenido.");
                return ServiceResult<ReservaDto>.Ok(MapReserva(existente));
            }

            foreach (var item in dto.Items.OrderBy(i => i.ProductoId))
            {
                var actualizados = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE Inventarios
                    SET StockReservado = StockReservado + {item.Cantidad}
                    WHERE ProductoId = {item.ProductoId}
                      AND StockReservado + {item.Cantidad} <= StockTotal", cancellationToken);

                if (actualizados != 1)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ServiceResult<ReservaDto>.Conflict("Stock insuficiente para uno o más productos. No se reservó ningún ítem.");
                }
            }

            var reserva = new Reserva
            {
                Id = Guid.NewGuid(),
                PedidoId = dto.PedidoId,
                Estado = EstadosReserva.Activa,
                FechaCreacion = DateTime.UtcNow,
                Detalles = dto.Items.Select(i => new ReservaDetalle
                {
                    ProductoId = i.ProductoId,
                    Cantidad = i.Cantidad
                }).ToList()
            };
            _db.Reservas.Add(reserva);
            await _db.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            _logger.LogInformation("Reserva {ReservaId} activa para el pedido {PedidoId}.", reserva.Id, reserva.PedidoId);
            return ServiceResult<ReservaDto>.Created(MapReserva(reserva));
        }
        catch (DbUpdateException)
        {
            await SafeRollbackAsync(transaccion, cancellationToken);
            _db.ChangeTracker.Clear();
            var again = await _db.Reservas.Include(r => r.Detalles)
                .FirstOrDefaultAsync(r => r.PedidoId == dto.PedidoId, cancellationToken);
            if (again is null)
                throw;
            if (!MismosItems(again, dto))
                return ServiceResult<ReservaDto>.Conflict("El pedidoId ya tiene una reserva con otro contenido.");
            return ServiceResult<ReservaDto>.Ok(MapReserva(again));
        }
        catch
        {
            await SafeRollbackAsync(transaccion, cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<ReservaDto>> LiberarAsync(Guid reservaId, CancellationToken cancellationToken)
    {
        await using var transaccion = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var reserva = await _db.Reservas.Include(r => r.Detalles)
                .FirstOrDefaultAsync(r => r.Id == reservaId, cancellationToken);
            if (reserva is null)
            {
                await transaccion.RollbackAsync(cancellationToken);
                return ServiceResult<ReservaDto>.NotFound("La reserva no existe.");
            }

            if (reserva.Estado == EstadosReserva.Liberada)
            {
                await transaccion.CommitAsync(cancellationToken);
                return ServiceResult<ReservaDto>.Ok(MapReserva(reserva));
            }

            foreach (var detalle in reserva.Detalles.OrderBy(d => d.ProductoId))
            {
                var actualizados = await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE Inventarios
                    SET StockReservado = StockReservado - {detalle.Cantidad}
                    WHERE ProductoId = {detalle.ProductoId}
                      AND StockReservado >= {detalle.Cantidad}", cancellationToken);

                if (actualizados != 1)
                {
                    await transaccion.RollbackAsync(cancellationToken);
                    return ServiceResult<ReservaDto>.Conflict("No se pudo liberar la reserva sin dejar el stock en negativo.");
                }
            }

            reserva.Estado = EstadosReserva.Liberada;
            await _db.SaveChangesAsync(cancellationToken);
            await transaccion.CommitAsync(cancellationToken);
            _logger.LogInformation("Reserva {ReservaId} liberada.", reserva.Id);
            return ServiceResult<ReservaDto>.Ok(MapReserva(reserva));
        }
        catch
        {
            await SafeRollbackAsync(transaccion, cancellationToken);
            throw;
        }
    }

    private static bool EsDeadlock(Exception ex)
    {
        for (var actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual.Message.Contains("deadlock", StringComparison.OrdinalIgnoreCase)
                || actual.Message.Contains("interbloqueo", StringComparison.OrdinalIgnoreCase))
                return true;
            if (actual.GetType().GetProperty("Number")?.GetValue(actual) is int numero && numero == 1205)
                return true;
        }

        return false;
    }

    private static bool MismosItems(Reserva reserva, CrearReservaDto dto) =>
        ReservaItems.SonIguales(
            reserva.Detalles.Select(d => (d.ProductoId, d.Cantidad)),
            dto.Items.Select(i => (i.ProductoId, i.Cantidad)));

    private static StockDto MapStock(InventarioItem item) =>
        new(item.ProductoId, item.StockTotal, item.StockReservado, item.StockTotal - item.StockReservado);

    private static ReservaDto MapReserva(Reserva reserva) => new(
        reserva.Id,
        reserva.PedidoId,
        reserva.Estado,
        reserva.FechaCreacion,
        reserva.Detalles
            .OrderBy(d => d.ProductoId)
            .Select(d => new ItemReservaDto(d.ProductoId, d.Cantidad))
            .ToList());

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaccion, CancellationToken cancellationToken)
    {
        try
        {
            await transaccion.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
