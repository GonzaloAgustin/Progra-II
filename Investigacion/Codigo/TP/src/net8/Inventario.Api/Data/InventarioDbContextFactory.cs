using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventario.Api.Data;

public class InventarioDbContextFactory : IDesignTimeDbContextFactory<InventarioDbContext>
{
    public InventarioDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<InventarioDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=ComercialInventario;User Id=sa;Password=Comercial_Dev123!;TrustServerCertificate=True;Encrypt=False")
            .Options;
        return new InventarioDbContext(options);
    }
}
