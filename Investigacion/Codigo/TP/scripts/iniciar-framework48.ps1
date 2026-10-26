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
    throw "No hay un SDK de .NET 8 para compilar los proyectos net48."
}

$dotnet = Find-DotNet8
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Get-CimInstance Win32_Process -Filter "Name = 'Productos.Api.exe' OR Name = 'Inventario.Api.exe' OR Name = 'Pedidos.Api.exe'" |
    Where-Object { $_.ExecutablePath -like "*\framework48\*" } |
    ForEach-Object {
        Write-Host "Cerrando $($_.Name) ($($_.ProcessId))"
        Stop-Process -Id $_.ProcessId -Force
    }

& $dotnet build (Join-Path $raiz "src\framework48\GestionComercial.Framework48.sln") -c Debug
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$ejecutables = @(
    "src\framework48\Productos.Api\bin\Debug\net48\Productos.Api.exe",
    "src\framework48\Inventario.Api\bin\Debug\net48\Inventario.Api.exe",
    "src\framework48\Pedidos.Api\bin\Debug\net48\Pedidos.Api.exe"
)

foreach ($relativo in $ejecutables) {
    $exe = Join-Path $raiz $relativo
    Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Normal
    Write-Host "Arrancando $exe"
}

Write-Host "Productos http://localhost:5011/swagger"
Write-Host "Inventario http://localhost:5012/swagger"
Write-Host "Pedidos http://localhost:5013/swagger"
Write-Host "Cada ventana espera ENTER para cerrarse."
