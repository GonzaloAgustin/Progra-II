using Inventario.Api.Services;
using Pedidos.Api.Services;

namespace Tests;

public class CalculoTests
{
    [Fact]
    public void Subtotal_y_total_se_calculan_en_el_servidor()
    {
        var teclado = CalculoPedido.Subtotal(2, 15000.00m);
        var mouse = CalculoPedido.Subtotal(1, 15000.00m);

        Assert.Equal(30000.00m, teclado);
        Assert.Equal(15000.00m, mouse);
        Assert.Equal(45000.00m, CalculoPedido.Total(new[] { teclado, mouse }));
    }

    [Fact]
    public void Rechaza_cantidad_o_precio_invalidos()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculoPedido.Subtotal(0, 10m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalculoPedido.Subtotal(1, 0m));
    }

    [Fact]
    public void Compara_items_de_reserva_sin_importar_el_orden()
    {
        var actuales = new[] { (10, 2), (15, 1) };
        var mismos = new[] { (15, 1), (10, 2) };
        var otros = new[] { (10, 3), (15, 1) };

        Assert.True(ReservaItems.SonIguales(actuales, mismos));
        Assert.False(ReservaItems.SonIguales(actuales, otros));
    }
}
