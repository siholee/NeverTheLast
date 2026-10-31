param(
    [Parameter(Mandatory = $true)][string]$Config,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$UnityExe,
    [int]$TimeoutSeconds = 1200
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$inputPath = [IO.Path]::GetFullPath($Config)
$outputPath = [IO.Path]::GetFullPath($Out)
if (Test-Path -LiteralPath $outputPath) { throw "기존 결과 폴더를 덮어쓰지 않습니다: $outputPath" }
if (-not $UnityExe) {
    $versionLine = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion: ' }
    $version = $versionLine.Substring('m_EditorVersion: '.Length).Trim()
    $UnityExe = "C:/Program Files/Unity $version/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityExe)) { throw "Unity 실행 파일이 없습니다: $UnityExe" }
if (-not (Test-Path -LiteralPath $inputPath)) { throw "실험 파일이 없습니다: $inputPath" }
$scratchParent = Join-Path $projectRoot '.codex-temp'
$scratchPath = [IO.Path]::GetFullPath((Join-Path $scratchParent ('BalanceSimParity-' + [Guid]::NewGuid().ToString('N'))))
New-Item -ItemType Directory -Path $outputPath | Out-Null
New-Item -ItemType Directory -Path $scratchPath | Out-Null
$ownedUnity = $null
$previousVerifyDirectory = $env:NTL_VERIFY_DIR
$exitCode = 1
function Get-ParityFingerprint {
    $paths = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Scripts') -Recurse -Filter '*.cs')
    $paths += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets/Resources/Data') -Filter '*.yaml')
    $paths += @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
    $lines = foreach ($file in $paths | Sort-Object FullName) { $file.FullName + '=' + (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    return ($lines -join "`n")
}
try {
    $before = Get-ParityFingerprint
    Copy-Item -LiteralPath $inputPath -Destination (Join-Path $outputPath 'experiment.json')
    $before | Set-Content -LiteralPath (Join-Path $outputPath 'source-manifest.txt') -Encoding utf8
    & dotnet build (Join-Path $PSScriptRoot 'BalanceSim.csproj') --nologo -v:q -p:UseSharedCompilation=false --configfile (Join-Path $PSScriptRoot 'NuGet.Config')
    if ($LASTEXITCODE -ne 0) { throw '콘솔 컴파일 실패' }
    $consoleDll = Join-Path $PSScriptRoot 'bin/Debug/net9.0/BalanceSim.dll'
    $consoleOut = Join-Path $scratchPath 'console'
    $consoleTraces = Join-Path $scratchPath 'console-traces'
    & dotnet $consoleDll run --config $inputPath --out $consoleOut --traces $consoleTraces
    if ($LASTEXITCODE -ne 0) { throw '콘솔 전투 오류' }
    $requestDirectory = Join-Path $scratchPath 'unity'
    New-Item -ItemType Directory -Path $requestDirectory | Out-Null
    @{ scenarios = @(@{ name = 'simulation_parity'; trigger = $inputPath; timeScale = 1 }); captureWidth = 640; captureHeight = 360; hideDebugBanner = $true; lineup = 'party5' } |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $requestDirectory 'request.json') -Encoding utf8
    $env:NTL_VERIFY_DIR = $requestDirectory
    $rawLog = Join-Path $scratchPath 'unity.log'
    $arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'), '-executeMethod', 'Tools.Editor.VerifyHarnessEditor.RunBatchRequest', '--ntl-verify-silent', '-logFile', ('"' + $rawLog + '"'))
    $ownedUnity = Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output "동등성 검사용 Unity PID $($ownedUnity.Id)"
    if (-not $ownedUnity.WaitForExit($TimeoutSeconds * 1000)) { throw "Unity 검사 시간 초과: ${TimeoutSeconds}초" }
    $runtimeResult = Get-Content -LiteralPath (Join-Path $requestDirectory 'result.json') -Raw | ConvertFrom-Json
    if ($ownedUnity.ExitCode -ne 0 -or -not $runtimeResult.success) { throw "Unity 실행 실패: $($runtimeResult.message)" }
    $runtimeErrors = @(Select-String -LiteralPath $rawLog -Pattern '^([A-Za-z_][\w.]*Exception):|error CS\d|\[ActionScheduler\].*(강제 종료|중단합니다)')
    if ($runtimeErrors.Count -gt 0) { throw ('Unity 예외/행동 강제 종료: ' + (($runtimeErrors | Select-Object -First 3 | ForEach-Object { $_.Line }) -join ' / ')) }
    if ($before -ne (Get-ParityFingerprint)) { throw '실행 도중 소스/데이터가 변경되어 결과를 승인할 수 없습니다.' }
    & dotnet $consoleDll compare --unity (Join-Path $requestDirectory 'unity-battles.jsonl') --sim (Join-Path $consoleOut 'battles.jsonl') --out (Join-Path $outputPath 'comparison') --unity-traces $requestDirectory --sim-traces $consoleTraces
    $exitCode = $LASTEXITCODE
    Copy-Item -LiteralPath (Join-Path $consoleOut 'manifest.json') -Destination (Join-Path $outputPath 'console-manifest.json')
    @{ unityExit = $ownedUnity.ExitCode; unitySuccess = $runtimeResult.success; message = $runtimeResult.message; fixedStep = .02; minimumFrameWallMilliseconds = 5; sourceUnchanged = $true; comparisonExit = $exitCode } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'execution.json') -Encoding utf8
}
catch {
    $diagnostics = @()
    $rawLog = Join-Path $scratchPath 'unity.log'
    if (Test-Path -LiteralPath $rawLog) { $diagnostics = @(Select-String -LiteralPath $rawLog -Pattern 'error CS|Exception|Aborting batchmode' | Select-Object -Last 12 | ForEach-Object { $_.Line }) }
    @{ success = $false; reason = $_.Exception.Message; diagnostics = $diagnostics } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputPath 'failure.json') -Encoding utf8
    Write-Error $_ -ErrorAction Continue
}
finally {
    $env:NTL_VERIFY_DIR = $previousVerifyDirectory
    if ($ownedUnity -and -not $ownedUnity.HasExited) { Stop-Process -Id $ownedUnity.Id -Force }
    # 실패한 실행도 개별 결과와 환경 요약을 보존한 뒤 원시 로그·요청을 정리한다.
    foreach ($pair in @(@('console/battles.jsonl', 'console-battles.jsonl'), @('unity/unity-battles.jsonl', 'unity-battles.jsonl'), @('unity/result.json', 'unity-execution.json'))) {
        $source = Join-Path $scratchPath $pair[0]
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $outputPath $pair[1]) }
    }
    $nativeSummaryPath = Join-Path $outputPath 'unity-execution.json'
    if (Test-Path -LiteralPath $nativeSummaryPath) {
        $nativeSummary = Get-Content -LiteralPath $nativeSummaryPath -Raw | ConvertFrom-Json
        $eventCounts = [ordered]@{}
        foreach ($field in @('visualEvents', 'vfxSlots', 'sfxEvents', 'portraitRects', 'uiRects')) {
            $eventCounts[$field] = @($nativeSummary.$field).Count
            $nativeSummary.PSObject.Properties.Remove($field)
        }
        $nativeSummary | Add-Member -NotePropertyName eventCounts -NotePropertyValue $eventCounts
        $nativeSummary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $nativeSummaryPath -Encoding utf8
    }
    $checkedScratch = [IO.Path]::GetFullPath($scratchPath)
    $allowedPrefix = [IO.Path]::GetFullPath($scratchParent).TrimEnd('\') + '\BalanceSimParity-'
    if (-not $checkedScratch.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw "정리 범위 밖: $checkedScratch" }
    if (Test-Path -LiteralPath $checkedScratch) { Remove-Item -LiteralPath $checkedScratch -Recurse -Force }
}
exit $exitCode
