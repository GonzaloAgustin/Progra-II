$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot

function Find-DotNet8 {
    $candidatos = @()
    $comando = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($comando) { $candidatos += $comando.Source }
    $candidatos += @(
        (Join-Path $env:ProgramFiles "dotnet\dotnet.exe"),
        (Join-Path $env:LOCALAPPDATA "dotnet-sdk8\dotnet.exe")
    )
    foreach ($ruta in ($candidatos | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique)) {
        $sdks = & $ruta --list-sdks 2>$null
        if ($sdks -match "(?m)^8\.") { return $ruta }
    }
    throw "No hay un SDK de .NET 8. Instalá el SDK 8.0 desde https://dotnet.microsoft.com/download/dotnet/8.0"
}

$dotnet = Find-DotNet8
Write-Host "Usando $dotnet"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$servicios = @(
    @{ Nombre = "Productos"; Proyecto = "src\net8\Productos.Api"; Perfil = "Productos.Api" },
    @{ Nombre = "Inventario"; Proyecto = "src\net8\Inventario.Api"; Perfil = "Inventario.Api" },
    @{ Nombre = "Pedidos"; Proyecto = "src\net8\Pedidos.Api"; Perfil = "Pedidos.Api" }
)

foreach ($servicio in $servicios) {
    $proyecto = Join-Path $raiz $servicio.Proyecto
    $argumentos = "run --project `"$proyecto`" --launch-profile $($servicio.Perfil)"
    Start-Process -FilePath $dotnet -ArgumentList $argumentos -WorkingDirectory $raiz -WindowStyle Normal
    Write-Host "Arrancando $($servicio.Nombre)"
}

Write-Host "Productos http://localhost:5001/swagger"
Write-Host "Inventario http://localhost:5002/swagger"
Write-Host "Pedidos http://localhost:5003/swagger"
Write-Host "La base por defecto es LocalDB. Docker Compose pisa esas cadenas."
