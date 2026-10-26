# 04. Comparación técnica

Comparación entre el código de `src/net8` y `src/framework48`, con las pruebas que sí se ejecutaron el 19 de octubre de 2026. El documento de investigación (`01`) no se reescribió: la portada sigue pendiente de institución, docente y los cuatro integrantes, y los resultados verificables están en este archivo.

## Evidencia

Host de compilación: SDK de .NET 8 en `%LOCALAPPDATA%\dotnet-sdk8\dotnet.exe`. El `dotnet` del PATH es el SDK 10 y no se usó para compilar `net8.0`. Base de las pruebas: LocalDB `(localdb)\MSSQLLocalDB`, una base distinta por contexto y por ejecución.

| Solución | Configuración | Resultado | Duración de la ejecución |
| --- | --- | --- | --- |
| `GestionComercial.Net8.sln` | Release | 16 superadas, 0 con error, 0 omitidas | 10 s |
| `GestionComercial.Framework48.sln` | Debug | 6 superadas, 0 con error, 0 omitidas | 2 min 41 s |

La corrida de Framework incluye filtro por categoría, reserva repetida, el mismo `pedidoId` con otro contenido (409), reserva concurrente, pedido con precio histórico, cancelación repetida, falta de stock, timeout de reserva, timeout de cancelación, Productos no disponible sin crear el pedido, y Swagger 2.0.

`docker compose` no se ejecutó. Además de las pruebas, el 19 de octubre de 2026 se levantaron los seis procesos. .NET 8 escuchó en 5001-5003 sobre LocalDB: OpenAPI 3 respondió 200, el catálogo de demostración devolvió Periféricos y el teclado de 15000, un pedido de 2 unidades quedó `CONFIRMADO` con total 30000 y, al cancelarlo, el stock disponible volvió a 10. Framework escuchó en 5011-5013 sobre bases propias (`ComercialProductosFw`, `ComercialInventarioFw`, `ComercialPedidosFw`): Swagger 2.0 respondió 200, y el mismo flujo de alta, reserva, confirmación y cancelación cerró en 30000. La UI de Swagger respondió HTTP 200; no se recorrió con el mouse. Las pruebas de Framework siguen usando `HttpServer` con la misma composición que el self-host.

## Arquitectura

Las dos soluciones tienen tres proyectos de API y un proyecto de pruebas. Cada API separa controllers, services, data, models y DTOs. Pedidos agrega clientes HTTP. No hay un proyecto de entidades compartidas: el mismo identificador numérico o GUID se interpreta en cada base.

.NET 8 es una aplicación web del SDK (`Microsoft.NET.Sdk.Web`, `net8.0`). Framework 4.8 es un ejecutable del SDK (`net48`) que llama a `WebApp.Start` y `UseWebApi`. `Global.asax` existe, pero el arranque comprobado por las pruebas no pasa por IIS.

## Dependencias

| Pieza | .NET 8 | Framework 4.8 |
| --- | --- | --- |
| HTTP | ASP.NET Core | Web API 2.2, paquete 5.2.9, self-host OWIN |
| ORM | EF Core SqlServer 8.0.11 | Entity Framework 6.4.4 |
| Documentación | Swashbuckle.AspNetCore 6.9.0 | Swashbuckle.Core 5.6.0 |
| JSON | el serializador de ASP.NET Core | Newtonsoft.Json 13.0.3, camelCase |
| DI | el contenedor del host | Autofac.WebApi2 6.1.0, `InstancePerRequest` |
| Pruebas | xUnit 2.9.2, Test SDK 17.11.1, Mvc.Testing 8.0.11 | xUnit 2.9.2, Test SDK 17.11.1, `HttpServer` |

## Controllers y configuración

.NET 8 usa `[Route]` y `[ApiController]`. El modelo inválido se convierte en `ValidationProblemDetails` con `traceId`. La configuración vive en `appsettings.json` y variables de entorno.

Framework usa `ApiController`, `[RoutePrefix]` y `WebApiConfig.Register`. El modelo inválido lo corta `ValidacionFilter` y arma un diccionario con `type`, `title`, `status`, `detail`, `instance` y `traceId`. La configuración vive en `App.config` (`connectionStrings` y `appSettings`). Hay un `Web.config` con los mismos valores para un hosting IIS que no se probó.

## Inyección de dependencias

En .NET 8 el `Program` registra el `DbContext`, los servicios y los clientes tipados. El host crea el alcance por request.

En Framework, `Composicion.Crear` arma un `HttpConfiguration`, registra controllers y servicios en Autofac y asigna `AutofacWebApiDependencyResolver`. Las pruebas que pasaron resuelven un contexto por request dentro de `HttpServer`, así que ese alcance funcionó en el host en memoria.

## Persistencia

Las dos versiones usan SQL Server. El nombre de categoría mide como máximo 150 caracteres en las dos, con `NombreNormalizado` y un índice único filtrado `WHERE Activo = 1`. El precio es `decimal(18,2)` y debe ser mayor que cero.

.NET 8 tiene migraciones de código generadas con `dotnet-ef` 8.0.11 (`Data/Migrations`, migración `Initial` en cada API). `ProductoId` del inventario no es identity. El arranque aplica `Database.Migrate` y reintenta solo errores transitorios de conexión.

Framework usa migraciones automáticas de EF6. El contexto no tiene constructor vacío: cada uno publica la cadena en una fábrica `IDbContextFactory` para que `MigrateDatabaseToLatestVersion` pueda construirlo. El `Seed` crea el índice filtrado y los `CHECK` de stock, precio y cantidades. La fábrica guarda la cadena en un campo estático. Las pruebas van en serie, una base por tipo de contexto. Dos contextos del mismo tipo en paralelo se pisarían la cadena.

## Comunicación HTTP

Inventario llama a `GET /api/productos/{id}/validacion`. Pedidos usa ese mismo contrato para precio y estado, y llama a Inventario para reservar y liberar.

.NET 8 registra clientes tipados con `IHttpClientFactory`, un handler de correlación y el timeout `Servicios:TimeoutSegundos` (5 segundos).

Framework guarda un `HttpClient` por dependencia, creado en la composición, con `BaseAddress` terminada en `/` y el mismo timeout. No usa `IHttpClientFactory`. El encabezado `X-Correlation-Id` lo agrega el handler de cada API y lo reenvían los clientes.

## OpenAPI y Swagger

.NET 8 publica OpenAPI 3 en `/swagger/v1/swagger.json` y la UI en `/swagger`. La prueba `Swagger_publica_openapi_3` y `Swagger_de_cada_servicio_responde` recibieron HTTP 200.

Framework publica Swagger 2.0 en `swagger/docs/v1` y la UI en `swagger`. La prueba `Swagger_es_2_y_el_catalogo_respeta_las_reglas` encontró `"swagger":"2.0"` y la ruta `/api/productos`. Newtonsoft escribe el JSON sin espacio después de los dos puntos.

## Errores

Los códigos coinciden: 200, 201 con `Location` cuando se crea, 204 en la baja lógica, 400, 404, 409, 500 y 503.

.NET 8 usa `ProblemDetails`. En 409 y 503 de pedidos, si la fila ya existe, el cuerpo incluye `pedidoId` y `estado`.

Framework usa el diccionario de `Errores.Crear`. Los mismos campos extra salen en el conflicto y en la dependencia no disponible cuando el pedido ya fue persistido. El filtro de excepción responde 500 sin la traza de la excepción.

## Concurrencia e idempotencia

La reserva corre en una transacción local `Serializable`. El descuento es un `UPDATE` condicional: solo suma stock reservado si el resultado no supera el total. Si un ítem no se actualiza, se revierte toda la reserva. Un deadlock (mensaje en inglés, en español, o número 1205) se reintenta hasta tres veces.

La primera reserva de un `pedidoId` responde 201. La misma lista, en cualquier orden, responde 200 y no vuelve a descontar. Otro contenido con el mismo `pedidoId` responde 409. Liberar dos veces deja el stock como estaba.

`PUT /api/inventario/{productoId}` fija el total. Si el total nuevo queda debajo de lo reservado, responde 409. Si no hay fila y el producto está activo, la crea. Si Productos no responde, Inventario responde 503.

## Pruebas

.NET 8 usa `WebApplicationFactory` y reemplaza el `DbContext` por LocalDB. Los clientes tipados apuntan a los `TestServer` de los otros servicios. Un handler puede simular 503 o timeout. El hosted service de reconciliación se quita y la prueba llama a `POST /api/pedidos/reconciliacion`.

Las 16 pruebas cubren el cálculo de subtotales, el catálogo (alta, filtro, precio inválido, categoría inactiva, baja lógica), Swagger de los tres servicios, stock, reserva de varios ítems, idempotencia, conflicto de contenido, rechazo total por falta de stock, dos reservas simultáneas, pedido con precio histórico y total 45000, cancelación repetida, timeout de reserva, timeout de cancelación y Productos no disponible sin crear el pedido.

Framework agrupa esos comportamientos en 6 pruebas, sobre `HttpServer` y LocalDB. Cubre el mismo flujo de negocio. No hay una prueba separada de `PUT` de producto: el alta de producto sí se ejecutó, y la modificación de un producto existente no tiene un caso propio en esta suite. La reserva concurrente exigió un 201 y un 409, con stock disponible 0. `Productos_no_disponible_no_crea_el_pedido` respondió 503 y el listado de pedidos quedó vacío.

## Configuración y despliegue

.NET 8 puede ir en Kestrel (puertos 5001-5003) o en el Compose (contenedor en 8080, publicado en esos mismos puertos). El Compose define SQL Server 2022, la contraseña local de `sa` y `depends_on` con healthcheck. Esa pila no se levantó, así que el healthcheck de `sqlcmd` y las imágenes `sdk:8.0` / `aspnet:8.0` quedan sin comprobación de ejecución.

Framework se ejecuta como tres `.exe` en Windows (puertos 5011-5013) contra LocalDB con seguridad integrada, en las bases `ComercialProductosFw`, `ComercialInventarioFw` y `ComercialPedidosFw`. .NET 8, en la misma instancia, usa los nombres sin sufijo. No hay Compose para esa versión. El camino IIS, con `Global.asax`, no se probó: ese archivo vuelve a registrar `WebApiConfig` sobre `GlobalConfiguration`, mientras el self-host usa otra `HttpConfiguration`.

## Limitaciones

- No hay una transacción entre las tres bases. La recuperación confirma el pedido si la reserva del mismo `pedidoId` ya existe, o lo rechaza si no hay stock y no quedó reserva. No resuelve por sí sola un corte de energía entre el commit de Inventario y el de Pedidos más allá de ese reintento.
- El proceso periódico corre cada 30 segundos. Las pruebas no esperan ese timer: llaman al endpoint.
- La fábrica estática de EF6 no admite dos bases del mismo contexto a la vez.
- El nombre de categoría quedó en 150 caracteres en las dos versiones. La especificación de .NET 8 pedía 100.
- La contraseña del Compose es de demostración.
- Falta ejecutar el Compose. Las dos pilas sí se arrancaron como procesos sobre LocalDB.
- La portada del documento de investigación sigue pendiente: institución, materia, docente, comisión, fecha y los cuatro integrantes.
