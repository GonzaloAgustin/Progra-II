using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity.Infrastructure;
using System.Data.Entity;
using System.Data.Entity.Infrastructure.Annotations;
using System.Data.Entity.Migrations;
using Inventario.Api.Models;

namespace Inventario.Api.Data;

public class InventarioContext : DbContext
{
    static InventarioContext()
    {
        Database.SetInitializer(new MigrateDatabaseToLatestVersion<InventarioContext, InventarioMigracion>());
    }

    public InventarioContext(string cadena) : base(cadena)
    {
        InventarioFactory.CadenaActual = cadena;
    }

    public DbSet<InventarioItem> Inventarios { get; set; }
    public DbSet<Reserva> Reservas { get; set; }
    public DbSet<ReservaDetalle> ReservaDetalles { get; set; }

    protected override void OnModelCreating(DbModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InventarioItem>().ToTable("Inventarios");
        modelBuilder.Entity<InventarioItem>().HasKey(i => i.ProductoId);
        modelBuilder.Entity<InventarioItem>().Property(i => i.ProductoId).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None);

        modelBuilder.Entity<Reserva>().ToTable("Reservas");
        modelBuilder.Entity<Reserva>().HasKey(r => r.Id);
        modelBuilder.Entity<Reserva>().Property(r => r.Id).HasDatabaseGeneratedOption(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.None);
        modelBuilder.Entity<Reserva>().Property(r => r.Estado).HasMaxLength(20).IsRequired();
        modelBuilder.Entity<Reserva>().Property(r => r.PedidoId).HasColumnAnnotation(
            IndexAnnotation.AnnotationName,
            new IndexAnnotation(new IndexAttribute("IX_Reservas_PedidoId") { IsUnique = true }));
        modelBuilder.Entity<Reserva>().HasMany(r => r.Detalles).WithRequired(d => d.Reserva).HasForeignKey(d => d.ReservaId).WillCascadeOnDelete(true);

        modelBuilder.Entity<ReservaDetalle>().ToTable("ReservaDetalles");
    }
}

public class InventarioFactory : IDbContextFactory<InventarioContext>
{
    public static string CadenaActual { get; set; }

    public InventarioContext Create() => new InventarioContext(CadenaActual);
}

public sealed class InventarioMigracion : DbMigrationsConfiguration<InventarioContext>
{
    public InventarioMigracion()
    {
        AutomaticMigrationsEnabled = true;
        AutomaticMigrationDataLossAllowed = false;
        ContextKey = "Inventario.Api.Data.InventarioContext";
    }

    protected override void Seed(InventarioContext context)
    {
        context.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Inventarios_Stock')
    ALTER TABLE Inventarios ADD CONSTRAINT CK_Inventarios_Stock CHECK (StockTotal >= 0 AND StockReservado >= 0 AND StockReservado <= StockTotal);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ReservaDetalles_Cantidad')
    ALTER TABLE ReservaDetalles ADD CONSTRAINT CK_ReservaDetalles_Cantidad CHECK (Cantidad > 0);");
    }
}
