e# Guía para presentar

Son tres APIs que trabajan juntas: Productos, Inventario y Pedidos. Hay dos versiones con el mismo comportamiento: .NET 8 y .NET Framework 4.8. Cada API tiene su propia base. No se comparten tablas.

## Arranque

En PowerShell, desde la carpeta del proyecto:

```powershell
.\scripts\iniciar-net8.ps1
.\scripts\iniciar-framework48.ps1
```

| Versión | Productos | Inventario | Pedidos |
| --- | --- | --- | --- |
| .NET 8 | http://localhost:5001/swagger | http://localhost:5002/swagger | http://localhost:5003/swagger |
| Framework 4.8 | http://localhost:5011/swagger | http://localhost:5012/swagger | http://localhost:5013/swagger |

.NET 8 usa LocalDB: `ComercialProductos`, `ComercialInventario`, `ComercialPedidos`. Framework usa otras tres bases, con sufijo `Fw`, porque cada versión crea el esquema con su propio Entity Framework. En Framework, cada ventana se cierra con ENTER.

## Qué decir al empezar

El cliente pide un producto. Pedidos consulta el precio vigente a Productos, guarda ese precio y pide a Inventario que reserve el stock. Si hay stock, el pedido queda confirmado. Si no hay, se rechaza entero. Si Inventario no responde, el pedido queda pendiente y después se puede reconciliar, sin descontar dos veces.

## Recorrido en Swagger

Mostrá .NET 8. En Development el catálogo de prueba trae cuatro productos: teclado, mouse, monitor y disco SSD. Si el listado está vacío, creá primero una categoría y un producto.

1. En Productos, `GET /api/productos`. Anotá el `id` del producto.
2. En Inventario, `PUT /api/inventario/{id}` con `{ "stockTotal": 10 }`. El total queda en 10. No se suma al anterior.
3. En Pedidos, `POST /api/pedidos` con `{ "items": [ { "productoId": 1, "cantidad": 2 } ] }`. Cambiá el `1` por el id real. La respuesta es 201, estado `CONFIRMADO` y total 30000. El precio lo puso el servidor.
4. En Inventario, `GET /api/inventario/{id}`. El disponible bajó a 8.
5. En Pedidos, `POST /api/pedidos/{id}/cancelacion`. El estado pasa a `CANCELADO`.
6. Volvé a consultar el stock. El disponible vuelve a 10.

Repetí el mismo recorrido en los puertos 5011, 5012 y 5013. En Framework, si no hay datos, creá antes la categoría y el producto:

- `POST /api/categorias` con `{ "nombre": "Perifericos", "descripcion": "Accesorios" }`
- `POST /api/productos` con `{ "categoriaId": 1, "nombre": "Teclado", "descripcion": "Mecanico", "precio": 15000 }`

## Diferencias que conviene nombrar

.NET 8 usa ASP.NET Core, Entity Framework Core, OpenAPI 3 y `IHttpClientFactory`. Framework 4.8 usa Web API 2, Entity Framework 6, Swagger 2.0 y un `HttpClient` reutilizable. Las reglas de negocio son las mismas. El nombre de categoría admite hasta 150 caracteres en las dos.

## Cierre

Las pruebas automáticas ya corrieron: 16 en .NET 8 y 6 en Framework 4.8. La comparación está en `docs/04_Comparacion_Tecnica.md`.

No muestres Docker: el archivo `docker-compose.yml` está, pero no se llegó a ejecutar. La demo es con LocalDB.
