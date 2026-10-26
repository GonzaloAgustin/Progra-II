$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot
$solucion = Join-Path $raiz "src\net8\GestionComercial.Net8.sln"

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
& $dotnet --version
& $dotnet test $solucion -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
