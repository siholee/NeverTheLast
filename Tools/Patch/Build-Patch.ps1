param(
    [Parameter(Mandatory = $true)][string]$BaseDir,
    [Parameter(Mandatory = $true)][string]$TargetDir,
    [Parameter(Mandatory = $true)][string]$FromVersion,
    [Parameter(Mandatory = $true)][string]$ToVersion,
    [Parameter(Mandatory = $true)][string]$OutputZip,
    [ValidateSet('Windows', 'Mac')][string]$Platform = 'Windows',
    [string]$XdeltaPath = '',
    [string]$BundledDecoderPath = ''
)

$ErrorActionPreference = 'Stop'
$baseRoot = (Resolve-Path -LiteralPath $BaseDir).Path
$targetRoot = (Resolve-Path -LiteralPath $TargetDir).Path
$outputPath = [IO.Path]::GetFullPath($OutputZip)
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$staging = Join-Path $tempRoot ("NeverTheLastPatch-" + [guid]::NewGuid().ToString('N'))
$packageName = "NeverTheLast_Patch_${FromVersion}_to_${ToVersion}_${Platform}"
$packageRoot = Join-Path $staging $packageName
$payloadRoot = Join-Path $packageRoot 'payload'
$resolvedXdelta = if ($XdeltaPath) { (Resolve-Path -LiteralPath $XdeltaPath).Path } else { '' }
$resolvedDecoder = if ($BundledDecoderPath) { (Resolve-Path -LiteralPath $BundledDecoderPath).Path } else { $resolvedXdelta }
$usesXdelta = $false

function Get-RelativeFileMap([string]$root) {
    $map = @{}
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        $map[$relative] = $file.FullName
    }
    return $map
}

function Get-Sha256([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

try {
    New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
    $baseFiles = Get-RelativeFileMap $baseRoot
    $targetFiles = Get-RelativeFileMap $targetRoot
    $files = [Collections.Generic.List[object]]::new()
    $deleted = [Collections.Generic.List[object]]::new()

    foreach ($relative in ($targetFiles.Keys | Sort-Object)) {
        $targetFile = $targetFiles[$relative]
        $targetHash = Get-Sha256 $targetFile
        $baseHash = $null
        if ($baseFiles.ContainsKey($relative)) {
            $baseHash = Get-Sha256 $baseFiles[$relative]
            if ($baseHash -eq $targetHash) { continue }
        }

        $mode = 'full'
        $payloadPath = $relative
        $destination = Join-Path $payloadRoot ($payloadPath.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if ($baseHash -and $resolvedXdelta -and (Get-Item -LiteralPath $targetFile).Length -ge 1MB) {
            $candidatePath = $relative + '.xdelta'
            $candidate = Join-Path $payloadRoot ($candidatePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
            New-Item -ItemType Directory -Path (Split-Path $candidate -Parent) -Force | Out-Null
            & $resolvedXdelta -f -e -s $baseFiles[$relative] $targetFile $candidate
            if ($LASTEXITCODE -ne 0) { throw "xdelta 생성 실패: $relative" }
            if ((Get-Item -LiteralPath $candidate).Length -lt (Get-Item -LiteralPath $targetFile).Length * 0.9) {
                $mode = 'xdelta'
                $payloadPath = $candidatePath
                $destination = $candidate
                $usesXdelta = $true
            }
            else {
                Remove-Item -LiteralPath $candidate -Force
            }
        }
        if ($mode -eq 'full') {
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $targetFile -Destination $destination -Force
        }
        $files.Add([ordered]@{
            path = $relative
            size = (Get-Item -LiteralPath $targetFile).Length
            mode = $mode
            payloadPath = $payloadPath
            payloadSha256 = Get-Sha256 $destination
            baseSha256 = $baseHash
            targetSha256 = $targetHash
        })
    }

    foreach ($relative in ($baseFiles.Keys | Sort-Object)) {
        if ($targetFiles.ContainsKey($relative)) { continue }
        $deleted.Add([ordered]@{
            path = $relative
            baseSha256 = Get-Sha256 $baseFiles[$relative]
        })
    }

    $manifest = [ordered]@{
        formatVersion = 1
        product = 'NeverTheLast'
        platform = $Platform
        fromVersion = $FromVersion
        toVersion = $ToVersion
        createdUtc = [DateTime]::UtcNow.ToString('O')
        files = $files
        deleted = $deleted
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $packageRoot 'patch-manifest.json') -Encoding UTF8

    $tsv = [Collections.Generic.List[string]]::new()
    foreach ($item in $files) {
        $base = if ($item.baseSha256) { $item.baseSha256 } else { '-' }
        $tsv.Add("file`t$($item.path)`t$base`t$($item.targetSha256)`t$($item.mode)`t$($item.payloadPath)`t$($item.payloadSha256)")
    }
    foreach ($item in $deleted) {
        $tsv.Add("delete`t$($item.path)`t$($item.baseSha256)`t-`t-`t-`t-")
    }
    Set-Content -LiteralPath (Join-Path $packageRoot 'patch-manifest.tsv') -Value $tsv -Encoding UTF8

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Apply-Patch.ps1') -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Apply-Patch.cmd') -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Apply-Patch.command') -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PATCH-README.txt') -Destination $packageRoot
    if ($usesXdelta) {
        $toolDir = Join-Path $packageRoot 'tools'
        New-Item -ItemType Directory -Path $toolDir -Force | Out-Null
        $decoderName = if ($Platform -eq 'Windows') { 'xdelta3.exe' } else { 'xdelta3' }
        Copy-Item -LiteralPath $resolvedDecoder -Destination (Join-Path $toolDir $decoderName)
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'XDELTA3-NOTICE.txt') -Destination $toolDir
    }

    New-Item -ItemType Directory -Path (Split-Path $outputPath -Parent) -Force | Out-Null
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $outputPath -CompressionLevel Optimal
    Write-Host "Patch: $outputPath"
    Write-Host "Changed/new: $($files.Count), deleted: $($deleted.Count)"
}
finally {
    $resolvedStaging = [IO.Path]::GetFullPath($staging)
    if ($resolvedStaging.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedStaging)) {
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
}
