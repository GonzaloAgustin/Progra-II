using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Data;

public static class DatabaseBootstrap
{
    public static async Task ApplyAsync(DbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        for (var intento = 1; intento <= 30; intento++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (intento < 30 && ex is not OperationCanceledException && EsTransitorio(ex))
            {
                logger.LogWarning("No se pudo migrar la base (intento {Intento}): {Mensaje}", intento, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private static bool EsTransitorio(Exception ex)
    {
        var texto = ex.ToString();
        return texto.Contains("network-related", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("server was not found", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("Login failed", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("Cannot open database", StringComparison.OrdinalIgnoreCase)
            || texto.Contains("error: 40", StringComparison.OrdinalIgnoreCase);
    }
}
