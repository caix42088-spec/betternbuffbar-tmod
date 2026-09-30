param(
    [string]$TmlDirectory = 'F:\1234\steamapps\common\tModLoader'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$qaRoot = Join-Path $projectRoot '.validation\UninterruptedUse\qa'
$source = Join-Path $qaRoot 'UninterruptedUseQA'
$profile = Join-Path $qaRoot 'profile'
$result = Join-Path $qaRoot 'checks.txt'
$dotnetPath = Join-Path $TmlDirectory 'dotnet\dotnet.exe'

New-Item -ItemType Directory -Force -Path $source, (Join-Path $profile 'Mods') | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'tests\UninterruptedUseQA\*') -Destination $source -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'UninterruptedUse\Localization') -Destination $source -Recurse -Force

# Test the production methods, not a reimplementation. Only remove client-side
# autoload attributes in this generated test copy so a headless host can load them.
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'UninterruptedUse\Common') -Recurse -Filter '*.cs' |
    ForEach-Object {
        $code = [IO.File]::ReadAllText($_.FullName)
        $code = $code.Replace('[Autoload(Side = ModSide.Client)]', '')
        [IO.File]::WriteAllText((Join-Path $source $_.Name), $code)
    }

$previousResult = [Environment]::GetEnvironmentVariable('UNINTERRUPTED_USE_QA_RESULT', 'Process')
$env:UNINTERRUPTED_USE_QA_RESULT = $result
Push-Location $TmlDirectory
try {
    & $dotnetPath 'tModLoader.dll' -server -build $source -tmlsavedirectory $profile 2>&1 |
        Tee-Object -FilePath (Join-Path $qaRoot 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "QA build failed: $LASTEXITCODE" }
    [IO.File]::WriteAllText((Join-Path $profile 'Mods\enabled.json'), '["UninterruptedUseQA"]')

    & $dotnetPath 'tModLoader.dll' -server -nosteam -tmlsavedirectory $profile 2>&1 |
        Tee-Object -FilePath (Join-Path $qaRoot 'run.log')
    if ($LASTEXITCODE -ne 0) { throw "QA execution failed: $LASTEXITCODE" }
    Get-Content -LiteralPath $result
}
finally {
    Pop-Location
    [Environment]::SetEnvironmentVariable('UNINTERRUPTED_USE_QA_RESULT', $previousResult, 'Process')
}
