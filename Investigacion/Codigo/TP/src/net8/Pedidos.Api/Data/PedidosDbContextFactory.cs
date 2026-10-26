using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pedidos.Api.Data;

public class PedidosDbContextFactory : IDesignTimeDbContextFactory<PedidosDbContext>
{
    public PedidosDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PedidosDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=ComercialPedidos;User Id=sa;Password=Comercial_Dev123!;TrustServerCertificate=True;Encrypt=False")
            .Options;
        return new PedidosDbContext(options);
    }
}
