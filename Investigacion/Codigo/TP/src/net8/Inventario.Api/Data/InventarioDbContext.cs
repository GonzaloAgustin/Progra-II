using Inventario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inventario.Api.Data;

public class InventarioDbContext : DbContext
{
    public InventarioDbContext(DbContextOptions<InventarioDbContext> options) : base(options)
    {
    }

    public DbSet<InventarioItem> Inventarios => Set<InventarioItem>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<ReservaDetalle> ReservaDetalles => Set<ReservaDetalle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventarioItem>(entity =>
        {
            entity.ToTable("Inventarios", tabla =>
            {
                tabla.HasCheckConstraint(
                    "CK_Inventarios_Stock",
                    "[StockTotal] >= 0 AND [StockReservado] >= 0 AND [StockReservado] <= [StockTotal]");
            });
            entity.HasKey(e => e.ProductoId);
            entity.Property(e => e.ProductoId).ValueGeneratedNever();
            entity.Property(e => e.Version).IsRowVersion();
        });

        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.ToTable("Reservas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Estado).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.PedidoId).IsUnique();
            entity.HasMany(e => e.Detalles)
                .WithOne(d => d.Reserva)
                .HasForeignKey(d => d.ReservaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReservaDetalle>(entity =>
        {
            entity.ToTable("ReservaDetalles", tabla =>
            {
                tabla.HasCheckConstraint("CK_ReservaDetalles_Cantidad", "[Cantidad] > 0");
            });
            entity.HasIndex(e => e.ReservaId);
        });
    }
}
