namespace Pedidos.Api.Services;

public sealed class ReconciliacionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReconciliacionHostedService> _logger;

    public ReconciliacionHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ReconciliacionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("Reconciliacion:Habilitada", true))
            return;

        var segundos = Math.Max(5, _configuration.GetValue("Reconciliacion:IntervaloSegundos", 30));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(segundos));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var servicio = scope.ServiceProvider.GetRequiredService<IPedidoService>();
                var resumen = await servicio.ReconciliarAsync(stoppingToken);
                if (resumen.PendientesRevisados > 0 || resumen.CancelacionesRevisadas > 0)
                {
                    _logger.LogInformation(
                        "Reconciliación: pendientes {Pendientes}, confirmados {Confirmados}, rechazados {Rechazados}, cancelados {Cancelados}, sin cambios {SinCambios}.",
                        resumen.PendientesRevisados,
                        resumen.Confirmados,
                        resumen.Rechazados,
                        resumen.Cancelados,
                        resumen.SinCambios);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Falló la reconciliación de pedidos.");
            }
        }
    }
}
