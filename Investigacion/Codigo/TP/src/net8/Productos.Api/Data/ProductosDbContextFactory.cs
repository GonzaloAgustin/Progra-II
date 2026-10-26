using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Productos.Api.Data;

public class ProductosDbContextFactory : IDesignTimeDbContextFactory<ProductosDbContext>
{
    public ProductosDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProductosDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=ComercialProductos;User Id=sa;Password=Comercial_Dev123!;TrustServerCertificate=True;Encrypt=False")
            .Options;
        return new ProductosDbContext(options);
    }
}
