$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot 'build\app\PaperNest.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Run build.ps1 first'}
$root=Join-Path $PSScriptRoot ('qa\core-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
foreach($test in @('self','autosave','paste','history','interaction','tray')){
    $report=Join-Path $root ($test+'.json')
    $process=Start-Process -FilePath $exe -ArgumentList ('--'+$test+'-test'),'--data-dir',('"'+(Join-Path $root $test)+'"'),'--report',('"'+$report+'"') -WindowStyle Hidden -PassThru
    if(!$process.WaitForExit(60000)){throw ($test+' timed out; process '+$process.Id)}
    if(!(Test-Path -LiteralPath $report)){throw ($test+' produced no report')}
    $result=Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
    if($process.ExitCode-ne0-or!$result.passed){throw ($test+': '+$result.error)}
    Write-Output ($test+': '+$result.checks.Count+' checks passed')
}
$crashData=Join-Path $root 'crash'
$ready=Join-Path $root 'crash-ready.txt'
$process=Start-Process -FilePath $exe -ArgumentList '--crash-write-test','--data-dir',('"'+$crashData+'"'),'--report',('"'+$ready+'"') -WindowStyle Hidden -PassThru -Wait
if($process.ExitCode-ne23){throw 'Crash writer did not exit as expected'}
$report=Join-Path $root 'crash-reopen.json'
$process=Start-Process -FilePath $exe -ArgumentList '--crash-reopen-test','--data-dir',('"'+$crashData+'"'),'--report',('"'+$report+'"') -WindowStyle Hidden -PassThru -Wait
$raw=Get-Content -LiteralPath $report -Raw
if($process.ExitCode-ne0){throw ('Abrupt-exit recovery failed: '+$raw)}
$result=$raw|ConvertFrom-Json
if(!$result.passed){throw ('Abrupt-exit recovery failed: '+$raw)}
Write-Output ('Abrupt-exit recovery passed. Reports: '+$root)
