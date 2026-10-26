# Sistema de Gestión Comercial

Tres microservicios independientes, con el mismo contrato funcional en dos stacks:

- `src/net8/GestionComercial.Net8.sln`: ASP.NET Core, Entity Framework Core 8, OpenAPI 3.
- `src/framework48/GestionComercial.Framework48.sln`: ASP.NET Web API 2 sobre .NET Framework 4.8, Entity Framework 6, Swagger 2.0.

Cada API tiene su propia base: `ComercialProductos`, `ComercialInventario` y `ComercialPedidos`. Las referencias entre servicios son lógicas (`ProductoId`, `PedidoId`, `ReservaId`).

Las decisiones que cierran diferencias entre las dos especificaciones están en `docs/decisiones.md`. La comparación con los resultados de las pruebas está en `docs/04_Comparacion_Tecnica.md`. Los documentos del trabajo (`00` a `03`) también están en `docs/`. La portada del documento `01` sigue para completarla con la institución, el docente y los cuatro integrantes.

## Requisitos

- Windows con .NET Framework 4.8 y SQL Server LocalDB (`(localdb)\MSSQLLocalDB`).
- SDK de .NET 8 para la solución moderna. En esta máquina el `dotnet` del PATH es el SDK 10, que no compila `net8.0`. El SDK 8 está en `%LOCALAPPDATA%\dotnet-sdk8\dotnet.exe`. El script `scripts\build-net8.ps1` busca un SDK 8.x y ejecuta las pruebas.
- Docker es opcional y solo cubre la versión .NET 8. Ese Compose no se ejecutó en esta verificación.

La contraseña `Comercial_Dev123!` es de demostración local para el usuario `sa` del contenedor. No es una credencial de un entorno real.

## Probar .NET 8

Desde la raíz del repositorio:

```powershell
.\scripts\build-net8.ps1
```

Eso restaura, compila en Release y corre xUnit contra LocalDB. Cada prueba crea bases con nombre único y las borra al terminar. El 19 de octubre de 2026 esa corrida terminó con 16 pruebas superadas y 0 con error, en unos 10 segundos de ejecución.

## Probar .NET Framework 4.8

Con el mismo SDK 8, que también hospeda el runner de `net48`:

```powershell
& "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe" test "src\framework48\GestionComercial.Framework48.sln" -c Debug
```

Las pruebas levantan las tres APIs en memoria con `HttpServer` y LocalDB. El 19 de octubre de 2026 la corrida completa terminó con 6 pruebas superadas y 0 con error, en 2 minutos 41 segundos de ejecución.

## Ejecutar .NET 8

Por defecto usa LocalDB, con las bases `ComercialProductos`, `ComercialInventario` y `ComercialPedidos`. En Development, `SeedDemoData` siembra la categoría Periféricos y un teclado de 15000.

```powershell
.\scripts\iniciar-net8.ps1
```

Puertos:

| Servicio | URL | Swagger UI | OpenAPI |
| --- | --- | --- | --- |
| Productos | http://localhost:5001 | http://localhost:5001/swagger | http://localhost:5001/swagger/v1/swagger.json |
| Inventario | http://localhost:5002 | http://localhost:5002/swagger | http://localhost:5002/swagger/v1/swagger.json |
| Pedidos | http://localhost:5003 | http://localhost:5003/swagger | http://localhost:5003/swagger/v1/swagger.json |

Ejemplo, con el SDK 8:

```powershell
$dotnet = "$env:LOCALAPPDATA\dotnet-sdk8\dotnet.exe"
& $dotnet run --project src\net8\Productos.Api --launch-profile Productos.Api
& $dotnet run --project src\net8\Inventario.Api --launch-profile Inventario.Api
& $dotnet run --project src\net8\Pedidos.Api --launch-profile Pedidos.Api
```

Al arrancar, cada API aplica sus migraciones de EF Core. El 19 de octubre de 2026 los tres procesos respondieron en 5001, 5002 y 5003: el catálogo de demostración, un pedido confirmado por 30000 y su cancelación, con el stock otra vez en 10.

Compose, todavía sin ejecutar en esta máquina:

```powershell
docker compose up --build
```

Publica SQL Server en 1433 y las tres APIs en 5001, 5002 y 5003. Dentro de la red, Productos e Inventario se llaman por el nombre del servicio y el puerto 8080.

## Ejecutar .NET Framework 4.8

La forma reproducible es el ejecutable con self-host OWIN. Cada proyecto lee `App.config`: cadena LocalDB, URL y, en Inventario y Pedidos, las direcciones de los otros servicios.

Puertos:

| Servicio | URL | Swagger UI | Documento Swagger 2.0 |
| --- | --- | --- | --- |
| Productos | http://localhost:5011/ | http://localhost:5011/swagger | http://localhost:5011/swagger/docs/v1 |
| Inventario | http://localhost:5012/ | http://localhost:5012/swagger | http://localhost:5012/swagger/docs/v1 |
| Pedidos | http://localhost:5013/ | http://localhost:5013/swagger | http://localhost:5013/swagger/docs/v1 |

```powershell
.\scripts\iniciar-framework48.ps1
```

Las bases son `ComercialProductosFw`, `ComercialInventarioFw` y `ComercialPedidosFw`. Van aparte de las de .NET 8 porque EF Core y EF6 no pueden compartir el mismo esquema. Cada ventana espera ENTER. El 19 de octubre de 2026 los tres procesos respondieron en 5011, 5012 y 5013: alta de categoría y producto, stock 10, pedido confirmado por 30000 y cancelación. Swagger 2.0 respondió 200 en los tres.

`Global.asax` queda para un hosting IIS. Esa vía no se ejecutó. El self-host y las pruebas usan la `HttpConfiguration` que arma `Composicion.Crear`.

## Recuperación de pedidos

Si Inventario no responde al reservar, el pedido queda `PENDIENTE` y la API responde 503 con `pedidoId` y `estado`. Repetir `POST /api/pedidos` crea otro pedido. La recuperación es `POST /api/pedidos/reconciliacion`, o el proceso periódico (30 segundos). La cancelación incierta queda en `CANCELACION_PENDIENTE` hasta que Inventario confirma la liberación.

En las pruebas ese proceso periódico está apagado y la conciliación se dispara con el endpoint.
