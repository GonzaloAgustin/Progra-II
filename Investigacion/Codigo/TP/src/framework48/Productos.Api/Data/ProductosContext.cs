using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using Productos.Api.Models;

namespace Productos.Api.Data;

public class ProductosContext : DbContext
{
    static ProductosContext()
    {
        Database.SetInitializer(new MigrateDatabaseToLatestVersion<ProductosContext, ProductosMigracion>());
    }

    public ProductosContext(string cadena) : base(cadena)
    {
        ProductosFactory.CadenaActual = cadena;
    }

    public DbSet<Categoria> Categorias { get; set; }
    public DbSet<Producto> Productos { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>().ToTable("Categorias");
        modelBuilder.Entity<Categoria>().Property(c => c.Nombre).HasMaxLength(150).IsRequired();
        modelBuilder.Entity<Categoria>().Property(c => c.NombreNormalizado).HasMaxLength(150).IsRequired();
        modelBuilder.Entity<Categoria>().Property(c => c.Descripcion).HasMaxLength(500);

        modelBuilder.Entity<Producto>().ToTable("Productos");
        modelBuilder.Entity<Producto>().Property(p => p.Nombre).HasMaxLength(150).IsRequired();
        modelBuilder.Entity<Producto>().Property(p => p.Descripcion).HasMaxLength(500);
        modelBuilder.Entity<Producto>().Property(p => p.Precio).HasPrecision(18, 2);
        modelBuilder.Entity<Producto>().Property(p => p.FechaCreacion).HasColumnType("datetime2");
        modelBuilder.Entity<Producto>()
            .HasRequired(p => p.Categoria)
            .WithMany(c => c.Productos)
            .HasForeignKey(p => p.CategoriaId)
            .WillCascadeOnDelete(false);
    }
}

public class ProductosFactory : IDbContextFactory<ProductosContext>
{
    public static string CadenaActual { get; set; }

    public ProductosContext Create() => new ProductosContext(CadenaActual);
}

public sealed class ProductosMigracion : DbMigrationsConfiguration<ProductosContext>
{
    public ProductosMigracion()
    {
        AutomaticMigrationsEnabled = true;
        AutomaticMigrationDataLossAllowed = false;
        ContextKey = "Productos.Api.Data.ProductosContext";
    }

    protected override void Seed(ProductosContext context)
    {
        context.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Categorias_NombreActivo')
    EXEC('CREATE UNIQUE INDEX IX_Categorias_NombreActivo ON Categorias(NombreNormalizado) WHERE Activo = 1');
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Productos_Precio')
    ALTER TABLE Productos ADD CONSTRAINT CK_Productos_Precio CHECK (Precio > 0);");
    }
}

public static class DemoSeeder
{
    public static void Sembrar(ProductosContext db)
    {
        var perifericos = AsegurarCategoria(db, "Periféricos", "Accesorios para PC");
        var monitores = AsegurarCategoria(db, "Monitores", "Pantallas");
        var almacenamiento = AsegurarCategoria(db, "Almacenamiento", "Discos y memoria");

        AsegurarProducto(db, perifericos.Id, "Teclado mecánico", "Teclado para PC", 15000m);
        AsegurarProducto(db, perifericos.Id, "Mouse inalámbrico", "Mouse para PC", 8000m);
        AsegurarProducto(db, monitores.Id, "Monitor 24 pulgadas", "Pantalla IPS", 90000m);
        AsegurarProducto(db, almacenamiento.Id, "Disco SSD 500 GB", "Disco de estado sólido", 45000m);
    }

    private static Categoria AsegurarCategoria(ProductosContext db, string nombre, string descripcion)
    {
        var normalizado = nombre.Trim().ToUpperInvariant();
        var existente = db.Categorias.FirstOrDefault(c => c.NombreNormalizado == normalizado);
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
        db.SaveChanges();
        return categoria;
    }

    private static void AsegurarProducto(ProductosContext db, int categoriaId, string nombre, string descripcion, decimal precio)
    {
        if (db.Productos.Any(p => p.CategoriaId == categoriaId && p.Nombre == nombre))
            return;

        db.Productos.Add(new Producto
        {
            CategoriaId = categoriaId,
            Nombre = nombre,
            Descripcion = descripcion,
            Precio = precio,
            Activo = true,
            FechaCreacion = System.DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
