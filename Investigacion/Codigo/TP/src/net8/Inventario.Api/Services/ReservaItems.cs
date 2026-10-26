namespace Inventario.Api.Services;

public static class ReservaItems
{
    public static bool SonIguales(IEnumerable<(int ProductoId, int Cantidad)> actuales, IEnumerable<(int ProductoId, int Cantidad)> solicitados)
    {
        var izquierda = actuales.OrderBy(i => i.ProductoId).ThenBy(i => i.Cantidad).ToArray();
        var derecha = solicitados.OrderBy(i => i.ProductoId).ThenBy(i => i.Cantidad).ToArray();
        return izquierda.SequenceEqual(derecha);
    }
}
