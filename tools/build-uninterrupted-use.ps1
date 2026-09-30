param(
    [string]$TmlDirectory = 'F:\1234\steamapps\common\tModLoader'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$source = Join-Path $projectRoot 'UninterruptedUse'
$validationRoot = Join-Path $projectRoot '.validation\UninterruptedUse'
$profile = Join-Path $validationRoot 'build-profile'
$output = Join-Path $projectRoot 'dist\UninterruptedUse.tmod'

if (!(Test-Path -LiteralPath (Join-Path $TmlDirectory 'tModLoader.dll'))) {
    throw 'tModLoader.dll not found. Pass your installation path with -TmlDirectory.'
}

$dotnetPath = Join-Path $TmlDirectory 'dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetPath)) {
    $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
}

New-Item -ItemType Directory -Force -Path $validationRoot, (Split-Path $output -Parent) | Out-Null
Push-Location $TmlDirectory
try {
    & $dotnetPath 'tModLoader.dll' -server -build $source -tmlsavedirectory $profile 2>&1 |
        Tee-Object -FilePath (Join-Path $validationRoot 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
}
finally {
    Pop-Location
}

$built = Join-Path $profile 'Mods\UninterruptedUse.tmod'
if (!(Test-Path -LiteralPath $built)) { throw 'The isolated .tmod output was not found.' }
Copy-Item -LiteralPath $built -Destination $output -Force
Write-Output "Built: $output"
