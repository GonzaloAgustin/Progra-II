using System;
using System.Collections.Generic;

namespace Productos.Api.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public string NombreNormalizado { get; set; }
    public string Descripcion { get; set; }
    public bool Activo { get; set; }
    public virtual ICollection<Producto> Productos { get; set; } = new List<Producto>();
}

public class Producto
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public virtual Categoria Categoria { get; set; }
    public string Nombre { get; set; }
    public string Descripcion { get; set; }
    public decimal Precio { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
