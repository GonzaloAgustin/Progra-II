using Microsoft.EntityFrameworkCore;
using Pedidos.Api.Models;

namespace Pedidos.Api.Data;

public class PedidosDbContext : DbContext
{
    public PedidosDbContext(DbContextOptions<PedidosDbContext> options) : base(options)
    {
    }

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoDetalle> PedidoDetalles => Set<PedidoDetalle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("Pedidos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Estado).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.HasIndex(e => e.Estado);
            entity.HasMany(e => e.Detalles)
                .WithOne(d => d.Pedido)
                .HasForeignKey(d => d.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PedidoDetalle>(entity =>
        {
            entity.ToTable("PedidoDetalles", tabla =>
            {
                tabla.HasCheckConstraint("CK_PedidoDetalles_Cantidad", "[Cantidad] > 0");
                tabla.HasCheckConstraint("CK_PedidoDetalles_Precio", "[PrecioUnitario] > 0");
            });
            entity.Property(e => e.PrecioUnitario).HasPrecision(18, 2);
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.HasIndex(e => e.PedidoId);
        });
    }
}
