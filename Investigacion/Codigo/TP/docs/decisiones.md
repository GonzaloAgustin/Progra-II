# Decisiones de implementación

Estas decisiones cierran diferencias entre el documento de .NET 8 y el de .NET Framework 4.8, o completan puntos que las especificaciones dejan abiertos. Las reglas de negocio no cambian: se eligió la variante compatible con las dos versiones.

## Nombre de categoría

El documento de .NET 8 define `Categorias.Nombre` como `NVARCHAR(100)`. El de Framework 4.8 lo define como `NVARCHAR(150)`. En las dos implementaciones el máximo es **150 caracteres**, igual que el nombre de producto. Así el contrato funcional es el mismo.

## Unicidad del nombre activo

La unicidad ignora mayúsculas y minúsculas. Para no depender de la collation de SQL Server se guarda `NombreNormalizado` (el nombre recortado, en mayúsculas invariantes) y un índice único filtrado por `Activo = 1`. Es un detalle de persistencia: la API sigue exponiendo `nombre`.

## Productos repetidos

Si un pedido o una reserva trae el mismo `productoId` más de una vez, se responde **400**. No se fusionan cantidades en silencio.

## Catálogo público y validación entre servicios

`GET /api/productos` y `GET /api/productos/{id}` muestran solo productos activos de categorías activas. Si el producto no está visible, el GET por id responde **404**.

Inventario y Pedidos tienen que distinguir “no existe” de “existe pero está inactivo”. Para eso Productos expone `GET /api/productos/{id}/validacion`, que devuelve precio, estado y si la categoría está activa. No es una pantalla de catálogo: es el contrato interno que usan los otros dos servicios.

## Reserva idempotente

La primera reserva de un `pedidoId` responde **201**. Repetirla con los mismos ítems responde **200** y no vuelve a descontar stock. El mismo `pedidoId` con otros ítems responde **409**. El documento de Framework no fija el código del reintento; se usa 200, como indica la especificación de .NET 8.

## Pedido creado con resultado incierto

El servidor genera el `pedidoId`. Si el cliente reintenta `POST /api/pedidos`, eso crea otro pedido. La reconciliación es del servidor, con el mismo `pedidoId`:

- Si Inventario no responde, el pedido queda `PENDIENTE` y la API responde **503** con `pedidoId` y `estado`.
- `POST /api/pedidos/reconciliacion`, o el proceso periódico, reintenta la reserva idempotente.
- Si la reserva ya existía, el pedido pasa a `CONFIRMADO` sin descontar de nuevo.
- Si no hay stock y no quedó reserva, pasa a `RECHAZADO`.

El mismo criterio vale para `CANCELACION_PENDIENTE`: no se marca `CANCELADO` hasta que Inventario confirma la liberación.

## Recuperación

Hay un proceso en segundo plano (`Reconciliacion:IntervaloSegundos`, 30 segundos por defecto) y el endpoint `POST /api/pedidos/reconciliacion` para ejecutarlo a demanda. No usa Kafka. Si Inventario reservó y Pedidos no llegó a confirmar, el reintento con el mismo `pedidoId` encuentra la reserva y confirma el pedido.

## Stock

`PUT /api/inventario/{productoId}` fija el total; no lo incrementa. Si no hay fila y el producto está activo, la crea. Si Productos no responde, Inventario responde **503** y no inventa el alta. La reserva de varios ítems es una transacción local: o se confirman todos o no se reserva ninguno. El descuento usa un `UPDATE` condicional (`stock reservado + cantidad <= stock total`) para que dos pedidos simultáneos no vendan de más. `ROWVERSION` queda en el modelo de SQL Server como control de concurrencia adicional.

## Errores

.NET 8 usa `ProblemDetails` y `ValidationProblemDetails`. Las extensiones `pedidoId`, `estado` y `traceId` viajan en el mismo JSON. Framework 4.8 usa un DTO con los mismos campos (`type`, `title`, `status`, `detail`, `instance`, `traceId`). Las respuestas no incluyen la traza interna.

## Bases locales

En LocalDB las dos implementaciones no pueden compartir el mismo archivo de base: EF Core y EF6 guardan el historial de migraciones de forma distinta y cada uno crea el esquema. .NET 8 usa `ComercialProductos`, `ComercialInventario` y `ComercialPedidos`. Framework 4.8 usa `ComercialProductosFw`, `ComercialInventarioFw` y `ComercialPedidosFw`. En Docker, solo corre .NET 8 y conserva los nombres sin sufijo.

## Fuera de alcance

Sin autenticación, API Gateway, Kafka, interfaz web, pagos ni facturación. La seguridad queda documentada como simplificación académica.

## Diferencias que se conservan

Entity Framework Core 8 frente a Entity Framework 6, OpenAPI 3 frente a Swagger 2.0, `IHttpClientFactory` frente a `HttpClient` reutilizable, y `ProblemDetails` nativo frente a un filtro de Web API 2. Esas diferencias son el objeto de la comparación, no defectos.
