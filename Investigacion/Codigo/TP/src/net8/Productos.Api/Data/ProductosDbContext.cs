using Microsoft.EntityFrameworkCore;
using Productos.Api.Models;

namespace Productos.Api.Data;

public class ProductosDbContext : DbContext
{
    public ProductosDbContext(DbContextOptions<ProductosDbContext> options) : base(options)
    {
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>(entity =>
        {
            entity.ToTable("Categorias");
            entity.Property(e => e.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(e => e.NombreNormalizado).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.HasIndex(e => e.NombreNormalizado)
                .IsUnique()
                .HasFilter("[Activo] = 1");
        });

        modelBuilder.Entity<Producto>(entity =>
        {
            entity.ToTable("Productos", tabla =>
            {
                tabla.HasCheckConstraint("CK_Productos_Precio", "[Precio] > 0");
            });
            entity.Property(e => e.Nombre).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.Precio).HasPrecision(18, 2);
            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).IsRequired();
            entity.HasIndex(e => e.CategoriaId);
            entity.HasOne(e => e.Categoria)
                .WithMany(c => c.Productos)
                .HasForeignKey(e => e.CategoriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
