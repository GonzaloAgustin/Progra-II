using Microsoft.EntityFrameworkCore;
using Pedidos.Api.Clients;
using Pedidos.Api.Data;
using Pedidos.Api.DTOs;
using Pedidos.Api.Models;

namespace Pedidos.Api.Services;

public interface IPedidoService
{
    Task<ServiceResult<IReadOnlyList<PedidoDto>>> ListarAsync(CancellationToken cancellationToken);
    Task<ServiceResult<PedidoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);
    Task<ServiceResult<PedidoDto>> CrearAsync(CrearPedidoDto dto, CancellationToken cancellationToken);
    Task<ServiceResult<PedidoDto>> CancelarAsync(Guid id, CancellationToken cancellationToken);
    Task<ReconciliacionDto> ReconciliarAsync(CancellationToken cancellationToken);
}

public sealed class PedidoService : IPedidoService
{
    private readonly PedidosDbContext _db;
    private readonly IProductosClient _productos;
    private readonly IInventarioClient _inventario;
    private readonly ILogger<PedidoService> _logger;

    public PedidoService(
        PedidosDbContext db,
        IProductosClient productos,
        IInventarioClient inventario,
        ILogger<PedidoService> logger)
    {
        _db = db;
        _productos = productos;
        _inventario = inventario;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<PedidoDto>>> ListarAsync(CancellationToken cancellationToken)
    {
        var pedidos = await _db.Pedidos.AsNoTracking()
            .Include(p => p.Detalles)
            .OrderByDescending(p => p.FechaCreacion)
            .ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<PedidoDto>>.Ok(pedidos.Select(Map).ToList());
    }

    public async Task<ServiceResult<PedidoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await _db.Pedidos.AsNoTracking()
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return pedido is null
            ? ServiceResult<PedidoDto>.NotFound("El pedido no existe.")
            : ServiceResult<PedidoDto>.Ok(Map(pedido));
    }

    public async Task<ServiceResult<PedidoDto>> CrearAsync(CrearPedidoDto dto, CancellationToken cancellationToken)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return ServiceResult<PedidoDto>.BadRequest("El pedido debe tener al menos un ítem.");
        if (dto.Items.Any(i => i.Cantidad <= 0 || i.ProductoId <= 0))
            return ServiceResult<PedidoDto>.BadRequest("Cada ítem debe tener producto y cantidad positivos.");
        if (dto.Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1))
            return ServiceResult<PedidoDto>.BadRequest("No se permiten productos repetidos en el pedido.");

        var detalles = new List<PedidoDetalle>();
        foreach (var item in dto.Items)
        {
            var producto = await _productos.ObtenerAsync(item.ProductoId, cancellationToken);
            if (producto.Estado == ConsultaEstado.Timeout)
                return ServiceResult<PedidoDto>.Unavailable("Productos no respondió a tiempo. No se creó el pedido.");
            if (producto.Estado == ConsultaEstado.NoDisponible)
                return ServiceResult<PedidoDto>.Unavailable("Productos no está disponible. No se creó el pedido.");
            if (producto.Estado == ConsultaEstado.NoEncontrado)
                return ServiceResult<PedidoDto>.BadRequest($"El producto {item.ProductoId} no existe.");
            if (!producto.Activo || !producto.CategoriaActiva)
                return ServiceResult<PedidoDto>.BadRequest($"El producto {item.ProductoId} no está disponible para la venta.");

            var subtotal = CalculoPedido.Subtotal(item.Cantidad, producto.Precio);
            detalles.Add(new PedidoDetalle
            {
                ProductoId = item.ProductoId,
                Cantidad = item.Cantidad,
                PrecioUnitario = producto.Precio,
                Subtotal = subtotal
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
        _logger.LogInformation("Pedido {PedidoId} persistido en PENDIENTE.", pedido.Id);

        return await IntentarReservarAsync(pedido, cancellationToken);
    }

    public async Task<ServiceResult<PedidoDto>> CancelarAsync(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await _db.Pedidos.Include(p => p.Detalles).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (pedido is null)
            return ServiceResult<PedidoDto>.NotFound("El pedido no existe.");

        if (pedido.Estado == EstadosPedido.Cancelado)
            return ServiceResult<PedidoDto>.Ok(Map(pedido));

        if (pedido.Estado == EstadosPedido.CancelacionPendiente)
            return await CompletarCancelacionAsync(pedido, cancellationToken);

        if (pedido.Estado != EstadosPedido.Confirmado)
            return ServiceResult<PedidoDto>.Conflict("Solo se pueden cancelar pedidos confirmados.");

        if (pedido.ReservaId is null)
            return ServiceResult<PedidoDto>.Conflict("El pedido confirmado no tiene una reserva asociada.");

        pedido.Estado = EstadosPedido.CancelacionPendiente;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Pedido {PedidoId} en CANCELACION_PENDIENTE.", pedido.Id);
        return await CompletarCancelacionAsync(pedido, cancellationToken);
    }

    public async Task<ReconciliacionDto> ReconciliarAsync(CancellationToken cancellationToken)
    {
        var pendientes = await _db.Pedidos.Include(p => p.Detalles)
            .Where(p => p.Estado == EstadosPedido.Pendiente)
            .ToListAsync(cancellationToken);

        var confirmados = 0;
        var rechazados = 0;
        var sinCambios = 0;
        foreach (var pedido in pendientes)
        {
            await IntentarReservarAsync(pedido, cancellationToken);
            if (pedido.Estado == EstadosPedido.Confirmado) confirmados++;
            else if (pedido.Estado == EstadosPedido.Rechazado) rechazados++;
            else sinCambios++;
        }

        var cancelaciones = await _db.Pedidos.Include(p => p.Detalles)
            .Where(p => p.Estado == EstadosPedido.CancelacionPendiente)
            .ToListAsync(cancellationToken);

        var cancelados = 0;
        foreach (var pedido in cancelaciones)
        {
            await CompletarCancelacionAsync(pedido, cancellationToken);
            if (pedido.Estado == EstadosPedido.Cancelado) cancelados++;
            else sinCambios++;
        }

        return new ReconciliacionDto(
            pendientes.Count,
            confirmados,
            rechazados,
            cancelaciones.Count,
            cancelados,
            sinCambios);
    }

    private async Task<ServiceResult<PedidoDto>> IntentarReservarAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        var items = pedido.Detalles.Select(d => (d.ProductoId, d.Cantidad)).ToList();
        var reserva = await _inventario.ReservarAsync(pedido.Id, items, cancellationToken);

        if (reserva.Estado == ResultadoRemoto.Confirmada)
        {
            pedido.Estado = EstadosPedido.Confirmado;
            pedido.ReservaId = reserva.ReservaId;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Pedido {PedidoId} CONFIRMADO con reserva {ReservaId}.", pedido.Id, reserva.ReservaId);
            return ServiceResult<PedidoDto>.Created(Map(pedido));
        }

        if (reserva.Estado == ResultadoRemoto.StockInsuficiente)
        {
            pedido.Estado = EstadosPedido.Rechazado;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Pedido {PedidoId} RECHAZADO por stock.", pedido.Id);
            return ServiceResult<PedidoDto>.Conflict("Stock insuficiente. El pedido fue rechazado y no quedó reserva parcial.")
                .With("pedidoId", pedido.Id)
                .With("estado", pedido.Estado);
        }

        _logger.LogWarning("Pedido {PedidoId} permanece PENDIENTE. Resultado de inventario: {Estado}.", pedido.Id, reserva.Estado);
        return ServiceResult<PedidoDto>.Unavailable("Inventario no confirmó la reserva. El pedido quedó pendiente de reconciliación.")
            .With("pedidoId", pedido.Id)
            .With("estado", pedido.Estado);
    }

    private async Task<ServiceResult<PedidoDto>> CompletarCancelacionAsync(Pedido pedido, CancellationToken cancellationToken)
    {
        if (pedido.ReservaId is null)
            return ServiceResult<PedidoDto>.Conflict("No hay reserva para liberar.").With("pedidoId", pedido.Id).With("estado", pedido.Estado);

        var liberacion = await _inventario.LiberarAsync(pedido.ReservaId.Value, cancellationToken);
        if (liberacion.Estado == ResultadoRemoto.Confirmada)
        {
            pedido.Estado = EstadosPedido.Cancelado;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Pedido {PedidoId} CANCELADO.", pedido.Id);
            return ServiceResult<PedidoDto>.Ok(Map(pedido));
        }

        if (liberacion.Estado == ResultadoRemoto.NoEncontrada)
        {
            return ServiceResult<PedidoDto>.Conflict("La reserva asociada no existe. La cancelación sigue pendiente.")
                .With("pedidoId", pedido.Id)
                .With("estado", pedido.Estado);
        }

        return ServiceResult<PedidoDto>.Unavailable("No se confirmó la liberación. El pedido sigue en cancelación pendiente.")
            .With("pedidoId", pedido.Id)
            .With("estado", pedido.Estado);
    }

    private static PedidoDto Map(Pedido pedido) => new(
        pedido.Id,
        pedido.Estado,
        pedido.Total,
        pedido.ReservaId,
        pedido.FechaCreacion,
        pedido.Detalles
            .OrderBy(d => d.ProductoId)
            .Select(d => new ItemPedidoDto(d.ProductoId, d.Cantidad, d.PrecioUnitario, d.Subtotal))
            .ToList());
}
