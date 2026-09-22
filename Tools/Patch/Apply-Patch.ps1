param([string]$GameDir = '')

$ErrorActionPreference = 'Stop'

# 실패해도 PowerShell 원본 오류 덤프 대신 한 줄짜리 사유만 보여 준다.
trap {
    Write-Host ''
    Write-Host "패치를 적용하지 못했습니다: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$manifestPath = Join-Path $PSScriptRoot 'patch-manifest.json'
$payloadRoot = Join-Path $PSScriptRoot 'payload'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'patch-manifest.json이 없습니다. 패치 ZIP을 통째로 다시 풀어 주세요.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

# 설치본을 알아보는 기준 파일 — 이전 버전에도 있던 파일 하나, 없으면 실행 파일.
$marker = ($manifest.files | Where-Object { $_.baseSha256 } | Select-Object -First 1).path
if (-not $marker) {
    $marker = if ($manifest.platform -eq 'Windows') { 'NeverTheLast.exe' } else { 'NeverTheLast.app' }
}
$marker = $marker.Replace('/', [IO.Path]::DirectorySeparatorChar)

# 패치 폴더째로 풀었든 게임 폴더에 바로 풀었든 설치본을 찾아낸다.
function Find-GameRoot {
    $candidate = $PSScriptRoot
    for ($depth = 0; $depth -lt 4 -and $candidate; $depth++) {
        if (Test-Path -LiteralPath (Join-Path $candidate $marker)) { return $candidate }
        $candidate = Split-Path $candidate -Parent
    }
    return $null
}

if ($GameDir) {
    $gameRoot = [IO.Path]::GetFullPath($GameDir)
}
else {
    $found = Find-GameRoot
    if (-not $found) {
        throw '게임 폴더를 찾지 못했습니다. 패치 파일을 NeverTheLast.exe가 있는 폴더(또는 그 안의 하위 폴더)에 풀어 주세요.'
    }
    $gameRoot = [IO.Path]::GetFullPath($found)
}

function Get-Sha256([string]$path) {
    # Get-FileHash 대신 .NET을 직접 쓴다 — 모듈 해석에 기대지 않고 대용량에서도 빠르다.
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($path)
        try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
        finally { $stream.Dispose() }
    }
    finally { $sha.Dispose() }
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
    throw "NeverTheLast.exe를 찾을 수 없습니다: $gameRoot"
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
    # payload에 version.txt가 있으면 그 바이트가 정답이다 — 여기서 다시 쓰면 BOM이 붙어
    # 다음 패치의 기준 해시와 어긋난다. 없을 때만 빌드와 같은 형식(BOM 없는 UTF-8 + CRLF)으로 남긴다.
    if (-not ($manifest.files | Where-Object { $_.path -eq 'version.txt' })) {
        [IO.File]::WriteAllText((Join-Path $gameRoot 'version.txt'), $manifest.toVersion + "`r`n",
            (New-Object Text.UTF8Encoding $false))
    }
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
