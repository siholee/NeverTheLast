param([string]$GameDir = (Split-Path $PSScriptRoot -Parent))

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'patch-manifest.json'
$payloadRoot = Join-Path $PSScriptRoot 'payload'
$gameRoot = [IO.Path]::GetFullPath($GameDir)
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

function Get-Sha256([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-Target([string]$relative) {
    $target = [IO.Path]::GetFullPath((Join-Path $gameRoot ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))))
    $prefix = $gameRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "허용되지 않은 패치 경로입니다: $relative"
    }
    return $target
}

if (-not (Test-Path -LiteralPath $gameRoot -PathType Container)) {
    throw "게임 폴더를 찾을 수 없습니다: $gameRoot"
}
if ($manifest.platform -eq 'Windows' -and -not (Test-Path -LiteralPath (Join-Path $gameRoot 'NeverTheLast.exe'))) {
    throw "NeverTheLast.exe를 찾을 수 없습니다. 패치 폴더를 기존 게임 폴더 안에 풀어 주세요."
}
if (Get-Process -Name 'NeverTheLast' -ErrorAction SilentlyContinue) {
    throw '게임을 종료한 뒤 패치를 다시 실행해 주세요.'
}

# 어떤 파일도 바꾸기 전에 설치본과 패치 payload를 전부 검증한다.
foreach ($item in $manifest.files) {
    $target = Get-Target $item.path
    $payloadRelative = if ($item.payloadPath) { $item.payloadPath } else { $item.path }
    $payload = Join-Path $payloadRoot ($payloadRelative.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $payloadHash = if ($item.payloadSha256) { $item.payloadSha256 } else { $item.targetSha256 }
    if (-not (Test-Path -LiteralPath $payload) -or (Get-Sha256 $payload) -ne $payloadHash) {
        throw "패치 파일이 손상되었습니다: $($item.path)"
    }
    if ($item.mode -eq 'xdelta' -and -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'tools\xdelta3.exe'))) {
        throw 'xdelta3 패치 도구가 없습니다.'
    }
    if (Test-Path -LiteralPath $target) {
        $current = Get-Sha256 $target
        if ($current -ne $item.targetSha256 -and (!$item.baseSha256 -or $current -ne $item.baseSha256)) {
            throw "설치본 버전 또는 파일이 일치하지 않습니다: $($item.path)"
        }
    }
    elseif ($item.baseSha256) {
        throw "기존 설치 파일이 없습니다: $($item.path)"
    }
}
foreach ($item in $manifest.deleted) {
    $target = Get-Target $item.path
    if ((Test-Path -LiteralPath $target) -and (Get-Sha256 $target) -ne $item.baseSha256) {
        throw "삭제 대상 파일이 기존 버전과 일치하지 않습니다: $($item.path)"
    }
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = Join-Path $gameRoot ".ntl-patch-backup-$($manifest.fromVersion)-to-$($manifest.toVersion)-$stamp"
$applied = [Collections.Generic.List[object]]::new()

try {
    New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
    foreach ($item in $manifest.files) {
        $target = Get-Target $item.path
        $payloadRelative = if ($item.payloadPath) { $item.payloadPath } else { $item.path }
        $payload = Join-Path $payloadRoot ($payloadRelative.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $alreadyCurrent = (Test-Path -LiteralPath $target) -and (Get-Sha256 $target) -eq $item.targetSha256
        if ($alreadyCurrent) { continue }

        $backup = Join-Path $backupRoot ($item.path.Replace('/', [IO.Path]::DirectorySeparatorChar))
        $existed = Test-Path -LiteralPath $target
        if ($existed) {
            New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $target -Destination $backup -Force
        }
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        if ($item.mode -eq 'xdelta') {
            $decoded = $target + '.ntl-patch-new'
            & (Join-Path $PSScriptRoot 'tools\xdelta3.exe') -f -d -s $target $payload $decoded
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $decoded) -or
                (Get-Sha256 $decoded) -ne $item.targetSha256) {
                if (Test-Path -LiteralPath $decoded) { Remove-Item -LiteralPath $decoded -Force }
                throw "바이너리 패치 복원에 실패했습니다: $($item.path)"
            }
            Move-Item -LiteralPath $decoded -Destination $target -Force
        }
        else {
            Copy-Item -LiteralPath $payload -Destination $target -Force
        }
        $applied.Add([pscustomobject]@{ target = $target; backup = $backup; existed = $existed })
    }

    foreach ($item in $manifest.deleted) {
        $target = Get-Target $item.path
        if (-not (Test-Path -LiteralPath $target)) { continue }
        $backup = Join-Path $backupRoot ($item.path.Replace('/', [IO.Path]::DirectorySeparatorChar))
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup -Force
        Remove-Item -LiteralPath $target -Force
        $applied.Add([pscustomobject]@{ target = $target; backup = $backup; existed = $true })
    }

    foreach ($item in $manifest.files) {
        $target = Get-Target $item.path
        if (-not (Test-Path -LiteralPath $target) -or (Get-Sha256 $target) -ne $item.targetSha256) {
            throw "패치 후 검증에 실패했습니다: $($item.path)"
        }
    }
    Set-Content -LiteralPath (Join-Path $gameRoot 'version.txt') -Value $manifest.toVersion -Encoding UTF8
    Write-Host "NeverTheLast $($manifest.toVersion) 패치가 완료되었습니다."
    Write-Host "복구용 백업: $backupRoot"
}
catch {
    for ($index = $applied.Count - 1; $index -ge 0; $index--) {
        $entry = $applied[$index]
        if ($entry.existed -and (Test-Path -LiteralPath $entry.backup)) {
            New-Item -ItemType Directory -Path (Split-Path $entry.target -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $entry.backup -Destination $entry.target -Force
        }
        elseif (Test-Path -LiteralPath $entry.target) {
            Remove-Item -LiteralPath $entry.target -Force
        }
    }
    throw
}
