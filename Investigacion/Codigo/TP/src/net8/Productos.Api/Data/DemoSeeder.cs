using Microsoft.EntityFrameworkCore;
using Productos.Api.Models;

namespace Productos.Api.Data;

public static class DemoSeeder
{
    public static async Task SeedAsync(ProductosDbContext db, CancellationToken cancellationToken = default)
    {
        var perifericos = await AsegurarCategoriaAsync(db, "Periféricos", "Accesorios para PC", cancellationToken);
        var monitores = await AsegurarCategoriaAsync(db, "Monitores", "Pantallas", cancellationToken);
        var almacenamiento = await AsegurarCategoriaAsync(db, "Almacenamiento", "Discos y memoria", cancellationToken);

        await AsegurarProductoAsync(db, perifericos.Id, "Teclado mecánico", "Teclado para PC", 15000m, cancellationToken);
        await AsegurarProductoAsync(db, perifericos.Id, "Mouse inalámbrico", "Mouse para PC", 8000m, cancellationToken);
        await AsegurarProductoAsync(db, monitores.Id, "Monitor 24 pulgadas", "Pantalla IPS", 90000m, cancellationToken);
        await AsegurarProductoAsync(db, almacenamiento.Id, "Disco SSD 500 GB", "Disco de estado sólido", 45000m, cancellationToken);
    }

    private static async Task<Categoria> AsegurarCategoriaAsync(ProductosDbContext db, string nombre, string descripcion, CancellationToken cancellationToken)
    {
        var normalizado = nombre.Trim().ToUpperInvariant();
        var existente = await db.Categorias.FirstOrDefaultAsync(c => c.NombreNormalizado == normalizado, cancellationToken);
        if (existente != null)
            return existente;

        var categoria = new Categoria
        {
            Nombre = nombre,
            NombreNormalizado = normalizado,
            Descripcion = descripcion,
            Activo = true
        };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(cancellationToken);
        return categoria;
    }

    private static async Task AsegurarProductoAsync(ProductosDbContext db, int categoriaId, string nombre, string descripcion, decimal precio, CancellationToken cancellationToken)
    {
        if (await db.Productos.AnyAsync(p => p.CategoriaId == categoriaId && p.Nombre == nombre, cancellationToken))
            return;

        db.Productos.Add(new Producto
        {
            CategoriaId = categoriaId,
            Nombre = nombre,
            Descripcion = descripcion,
            Precio = precio,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
