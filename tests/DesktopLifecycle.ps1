param([switch]$Installed,[string]$ExpectedStateFile,[string]$Report)
$ErrorActionPreference='Stop'
if(!$Installed -or !$ExpectedStateFile -or !$Report){throw 'Explicitly pass -Installed -ExpectedStateFile <original notes.json> -Report <output.json>. This test exits and reopens the installed desktop app.'}
$projectRoot=Split-Path -Parent $PSScriptRoot
$testRoot=Join-Path $projectRoot ('qa\desktop-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$launch=Join-Path $testRoot 'DesktopLaunch.exe'
$probe=Join-Path $testRoot 'StorageProbe.exe'
& $compiler /nologo /target:exe /reference:Microsoft.CSharp.dll ('/out:'+$launch) (Join-Path $PSScriptRoot 'DesktopLaunch.cs') (Join-Path $projectRoot 'src\WindowsHost.cs') (Join-Path $projectRoot 'src\L.cs')
if($LASTEXITCODE -ne 0){throw 'Desktop launcher did not compile'}
& $compiler /nologo /target:winexe /reference:Microsoft.CSharp.dll /reference:System.Runtime.Serialization.dll /reference:System.Web.Extensions.dll ('/out:'+$probe) (Join-Path $PSScriptRoot 'StorageProbe.cs') (Join-Path $projectRoot 'src\WindowsHost.cs') (Join-Path $projectRoot 'src\L.cs') (Join-Path $projectRoot 'src\Model.cs')
if($LASTEXITCODE -ne 0){throw 'Storage probe did not compile'}
$app=Join-Path $env:LOCALAPPDATA 'Programs\PaperNest\PaperNest.exe'
$dataRoot=Join-Path $env:LOCALAPPDATA 'PaperNest\Data'
$shortcut=Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) '纸间 · 桌面便签.lnk'
$expected=Get-Content -LiteralPath $ExpectedStateFile -Raw|ConvertFrom-Json
function Wait-Json([string]$path){$deadline=[datetime]::UtcNow.AddSeconds(8);while(!(Test-Path -LiteralPath $path) -and [datetime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100};return Get-Content -LiteralPath $path -Raw|ConvertFrom-Json}
function Inspect-Live([string]$name){$path=Join-Path $testRoot ($name+'.json');& $launch $app --inspect-output $path|Out-Null;return Wait-Json $path}
$results=[Collections.Generic.List[object]]::new()
$state=Inspect-Live 'initial'
for($cycle=1;$cycle -le 3;$cycle++){
 $pidBefore=$state.processId
 & $launch $app --exit|Out-Null
 $deadline=[datetime]::UtcNow.AddSeconds(8)
 while((Get-Process -Id $pidBefore -ErrorAction SilentlyContinue) -and [datetime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(Get-Process -Id $pidBefore -ErrorAction SilentlyContinue){throw 'Complete exit left the desktop process running'}
 if($cycle -eq 2){$starter=Start-Process -FilePath $app -WindowStyle Hidden -PassThru -Wait;$method='direct from development host; storage handoff'}else{& $launch $shortcut|Out-Null;$method='Windows desktop shortcut'}
 Start-Sleep -Milliseconds 500
 $state=Inspect-Live ('cycle-'+$cycle)
 if($state.runtimeVersion -ne [Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion -or $state.physicalStatePath -ne (Join-Path $dataRoot 'notes.json') -or !$state.saved){throw 'Restart loaded the wrong runtime or physical data file'}
 foreach($note in $expected.Notes){if(!$state.noteIds.Contains($note.Id)){throw 'An original note disappeared after a complete exit'}}
 $snapshot=Join-Path $testRoot ('snapshot-'+$cycle)
 $storageReport=Join-Path $testRoot ('storage-'+$cycle+'.json')
 & $launch $probe $dataRoot $storageReport $snapshot|Out-Null
 $storage=Wait-Json $storageReport
 $saved=Get-Content -LiteralPath (Join-Path $snapshot 'notes.json') -Raw|ConvertFrom-Json
 foreach($note in $expected.Notes){
  $actual=$saved.Notes|Where-Object {$_.Id -eq $note.Id}
  if($actual.Title -ne $note.Title -or $actual.Text.TrimEnd() -ne $note.Text.TrimEnd() -or $actual.Checks -ne $note.Checks -or $actual.ImageFile -ne $note.ImageFile -or $actual.Hidden -ne $note.Hidden){throw ('Restart changed original content or visibility: '+$note.Id)}
  foreach($property in @('Left','Top','Width','Height','Zoom','Angle')){if([math]::Abs($actual.$property-$note.$property) -gt 0.00001){throw ('Restart changed saved layout: '+$property)}}
  if($note.ImageFile){$sourceImage=Join-Path (Split-Path -Parent $ExpectedStateFile) ('Images\'+$note.ImageFile);$savedImage=Join-Path $snapshot ('Images\'+$note.ImageFile);if((Get-FileHash -LiteralPath $sourceImage).Hash -ne (Get-FileHash -LiteralPath $savedImage).Hash){throw 'Restart changed the original image bytes'}}
 }
 $results.Add([pscustomobject]@{cycle=$cycle;method=$method;newProcess=($pidBefore -ne $state.processId);notes=$state.notes;visible=$state.visible;originalContentAndLayoutPreserved=$true;physicalStatePath=$state.physicalStatePath})
}
[pscustomobject]@{passed=$true;checks=$results;testRoot=$testRoot;finished=[datetime]::Now.ToString('s')}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $Report -Encoding utf8
Get-Content -LiteralPath $Report -Raw
