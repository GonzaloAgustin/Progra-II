namespace Pedidos.Api.Services;

public static class CalculoPedido
{
    public static decimal Subtotal(int cantidad, decimal precioUnitario)
    {
        if (cantidad <= 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser positiva.");
        if (precioUnitario <= 0)
            throw new ArgumentOutOfRangeException(nameof(precioUnitario), "El precio debe ser mayor que cero.");

        return decimal.Round(cantidad * precioUnitario, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal Total(IEnumerable<decimal> subtotales) =>
        decimal.Round(subtotales.Sum(), 2, MidpointRounding.AwayFromZero);
}
