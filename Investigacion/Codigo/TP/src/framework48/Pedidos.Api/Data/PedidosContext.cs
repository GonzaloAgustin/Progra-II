using System.Data.Entity.Infrastructure;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using Pedidos.Api.Models;

namespace Pedidos.Api.Data;

public class PedidosContext : DbContext
{
    static PedidosContext()
    {
        Database.SetInitializer(new MigrateDatabaseToLatestVersion<PedidosContext, PedidosMigracion>());
    }

    public PedidosContext(string cadena) : base(cadena)
    {
        PedidosFactory.CadenaActual = cadena;
    }

    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<PedidoDetalle> PedidoDetalles { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>().ToTable("Pedidos");
        modelBuilder.Entity<Pedido>().HasKey(p => p.Id);
        modelBuilder.Entity<Pedido>().Property(p => p.Id).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None);
        modelBuilder.Entity<Pedido>().Property(p => p.Estado).HasMaxLength(30).IsRequired();
        modelBuilder.Entity<Pedido>().Property(p => p.Total).HasPrecision(18, 2);
        modelBuilder.Entity<Pedido>().HasMany(p => p.Detalles).WithRequired(d => d.Pedido).HasForeignKey(d => d.PedidoId).WillCascadeOnDelete(true);

        modelBuilder.Entity<PedidoDetalle>().ToTable("PedidoDetalles");
        modelBuilder.Entity<PedidoDetalle>().Property(d => d.PrecioUnitario).HasPrecision(18, 2);
        modelBuilder.Entity<PedidoDetalle>().Property(d => d.Subtotal).HasPrecision(18, 2);
    }
}

public class PedidosFactory : IDbContextFactory<PedidosContext>
{
    public static string CadenaActual { get; set; }

    public PedidosContext Create() => new PedidosContext(CadenaActual);
}

public sealed class PedidosMigracion : DbMigrationsConfiguration<PedidosContext>
{
    public PedidosMigracion()
    {
        AutomaticMigrationsEnabled = true;
        AutomaticMigrationDataLossAllowed = false;
        ContextKey = "Pedidos.Api.Data.PedidosContext";
    }

    protected override void Seed(PedidosContext context)
    {
        context.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PedidoDetalles_Cantidad')
    ALTER TABLE PedidoDetalles ADD CONSTRAINT CK_PedidoDetalles_Cantidad CHECK (Cantidad > 0);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PedidoDetalles_Precio')
    ALTER TABLE PedidoDetalles ADD CONSTRAINT CK_PedidoDetalles_Precio CHECK (PrecioUnitario > 0);");
    }
}
