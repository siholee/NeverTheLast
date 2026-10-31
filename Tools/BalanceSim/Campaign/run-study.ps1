param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$Out,
    [Parameter(Mandatory=$true)][string]$Scratch,
    [int]$Workers=8,
    [int]$Runs=10,
    [int]$StageCap=340
)
$ErrorActionPreference='Stop'
$playerPath=[IO.Path]::GetFullPath($Player)
$outputPath=[IO.Path]::GetFullPath($Out)
$scratchPath=[IO.Path]::GetFullPath($Scratch)
$catalog=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'intent-parties.json') -Raw|ConvertFrom-Json
if(-not (Test-Path -LiteralPath $playerPath)){throw '전용 QA Player가 없습니다.'}
if($Workers -lt 1 -or $Workers -gt 32 -or $Runs -lt 1 -or $StageCap -lt 1){throw '실험 범위 오류'}
New-Item -ItemType Directory -Path $outputPath -Force|Out-Null
New-Item -ItemType Directory -Path $scratchPath -Force|Out-Null
$jobs=@()
try {
    for($index=0;$index -lt $Workers;$index++) {
        $directory=Join-Path $scratchPath ('worker'+$index.ToString('D2'))
        if(Test-Path -LiteralPath $directory){throw "기존 작업을 덮어쓰지 않습니다: $directory"}
        New-Item -ItemType Directory -Path $directory|Out-Null
        $teams=@($catalog.teams|Where-Object { [Array]::IndexOf($catalog.teams,$_) % $Workers -eq $index })
        @{teams=$teams}|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $directory 'intent-parties.json') -Encoding utf8
        @{scenarios=@(@{name='composition_campaign';trigger='intent_study';lineup='party5';partyLevel=$Runs;stage=$StageCap});hideDebugBanner=$true;captureWidth=640;captureHeight=360}|
            ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $directory 'request.json') -Encoding utf8
        $arguments=@('-batchmode','-nographics','-job-worker-count','1','-screen-width','640','-screen-height','360','--ntl-verify-silent','--ntl-verify-dir',('"'+$directory+'"'),'-logFile',('"'+(Join-Path $directory 'player.log')+'"'))
        $process=Start-Process -FilePath $playerPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
        $jobs+=@{worker=$index;directory=$directory;process=$process;expected=$teams.Count*$Runs}
        @{pid=$process.Id;directory=$directory;teams=@($teams.key);runs=$Runs;stageCap=$StageCap}|ConvertTo-Json -Depth 5|
            Set-Content -LiteralPath (Join-Path $directory 'ownership.json') -Encoding utf8
        Write-Output "worker $index PID $($process.Id): $($teams.Count)개 편성 × $Runs"
    }
    $lastReport=[DateTime]::MinValue
    while(@($jobs|Where-Object {-not $_.process.HasExited}).Count -gt 0) {
        if(([DateTime]::UtcNow-$lastReport).TotalSeconds -ge 30){
            $progress=foreach($job in $jobs){
                $path=Join-Path $job.directory 'runs.jsonl'
                $count=if(Test-Path -LiteralPath $path){@(Get-Content -LiteralPath $path).Count}else{0}
                @{worker=$job.worker;completed=$count;expected=$job.expected;running=(-not $job.process.HasExited)}
            }
            $progress|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $scratchPath 'progress.json') -Encoding utf8
            Write-Output ('완료 '+(($progress|Measure-Object completed -Sum).Sum)+'/'+($catalog.teams.Count*$Runs))
            $lastReport=[DateTime]::UtcNow
        }
        Start-Sleep -Seconds 1
    }
    $execution=foreach($job in $jobs){
        $destination=Join-Path $outputPath ('worker'+$job.worker.ToString('D2'))
        New-Item -ItemType Directory -Path $destination|Out-Null
        foreach($name in @('runs.jsonl','battles.jsonl','rests.jsonl','theme_recoveries.jsonl','catalog.json','intent-parties.json','reward_rejections.jsonl','reward_fallbacks.jsonl')){
            $source=Join-Path $job.directory $name
            if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination (Join-Path $destination $name)}
        }
        $resultPath=Join-Path $job.directory 'result.json'
        if(-not (Test-Path -LiteralPath $resultPath)){$resultPath=Join-Path $job.directory '.runtime-result.json'}
        $success=$false
        if(Test-Path -LiteralPath $resultPath){
            $result=Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json
            $counts=[ordered]@{}
            foreach($field in @('visualEvents','vfxSlots','sfxEvents','portraitRects','uiRects')){
                $counts[$field]=@($result.$field).Count;$result.PSObject.Properties.Remove($field)
            }
            $result|Add-Member -NotePropertyName eventCounts -NotePropertyValue $counts
            $result|ConvertTo-Json -Depth 20|Set-Content -LiteralPath (Join-Path $destination 'execution.json') -Encoding utf8
            $success=$result.success
        }
        @{worker=$job.worker;pid=$job.process.Id;exit=$job.process.ExitCode;success=$success;expected=$job.expected}
    }
    $execution|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $outputPath 'execution.json') -Encoding utf8
    if(@($execution|Where-Object {-not $_.success -or $_.exit -ne 0}).Count -gt 0){exit 2}
}
finally {
    foreach($job in $jobs){if(-not $job.process.HasExited){Stop-Process -Id $job.process.Id -Force}}
}
