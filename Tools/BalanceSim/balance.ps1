param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'BalanceSim.csproj'
$configPath = Join-Path $PSScriptRoot 'NuGet.Config'
& dotnet build $projectPath --nologo -v:q -p:UseSharedCompilation=false --configfile $configPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet (Join-Path $PSScriptRoot 'bin/Debug/net9.0/BalanceSim.dll') @Arguments
exit $LASTEXITCODE
