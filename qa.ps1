$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$qaRoot = Join-Path $projectRoot ('qa\run-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qaRoot -Force | Out-Null
$target = Join-Path $qaRoot 'PaperNest-QA'
$installer = Join-Path $projectRoot 'build\PaperNest-Setup.exe'
$results = [Collections.Generic.List[string]]::new()
function Assert-Paper([bool]$condition,[string]$message) { if (-not $condition) { throw $message }; $results.Add($message) }
function Wait-PaperFile([string]$path) {
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $path)) { if ([DateTime]::UtcNow -gt $deadline) { throw ('Timed out: ' + $path) }; Start-Sleep -Milliseconds 100 }
}
function Invoke-PaperCommand([string]$command) {
    $p = Start-Process -FilePath $appExe -ArgumentList $command, '--data-dir', ('"'+$dataRoot+'"') -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit(7000)) { throw ('IPC command did not finish: ' + $command) }
    if ($p.ExitCode -ne 0) { throw ('IPC command failed: ' + $command) }
}
function Read-PaperInspection {
    $path = Join-Path $dataRoot 'inspection.json'
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    Invoke-PaperCommand '--inspect'
    Wait-PaperFile $path
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}
$installReport = Join-Path $qaRoot 'install.json'
$install = Start-Process -FilePath $installer -ArgumentList '--quiet','--test','--no-launch','--target',('"'+$target+'"'),'--report',('"'+$installReport+'"') -WindowStyle Hidden -PassThru -Wait
Assert-Paper ($install.ExitCode -eq 0) 'installer extracts payload successfully'
Wait-PaperFile $installReport
Assert-Paper ((Get-Content -LiteralPath $installReport -Raw | ConvertFrom-Json).success) 'installer reports success'
$appExe = Join-Path $target 'PaperNest.exe'
Assert-Paper ((Test-Path -LiteralPath $appExe) -and (Test-Path -LiteralPath (Join-Path $target 'Fonts\LXGWWenKaiLite-Regular.ttf'))) 'installed app includes handwriting font'
Assert-Paper (Test-Path -LiteralPath (Join-Path $target 'TestLinks\纸间.lnk')) 'desktop shortcut has been created'
Assert-Paper ((Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PaperNest.DesktopNotes.QA').InstallLocation -eq $target) 'Windows uninstall registration points to correct app directory'
$menuRoot='HKCU:\Software\PaperNest\QA\Classes\DesktopBackground\Shell\PaperNest.DesktopNotes'
$menuVerbs='HKCU:\Software\PaperNest\QA\Classes\PaperNest.DesktopNotes.ContextMenu\shell'
Assert-Paper ((Get-ItemProperty -LiteralPath $menuRoot).MUIVerb -in @('纸间便签','PaperNest')) 'installer adds owned desktop context submenu in isolated registry'
foreach($kind in @('text','checklist','photo')){
 $verb=Get-ChildItem -LiteralPath $menuVerbs|Where-Object{$_.PSChildName.EndsWith('_'+$kind)}
 $command=(Get-Item -LiteralPath (Join-Path $verb.PSPath 'command')).GetValue('')
 Assert-Paper ($command-eq('"'+$appExe+'" --new-'+$kind)) ('desktop '+$kind+' verb points to the quoted installed executable')
}
$menuControl=Join-Path $qaRoot 'MenuControl.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe ('/out:'+$menuControl) (Join-Path $projectRoot 'tests\MenuControl.cs') (Join-Path $projectRoot 'src\DesktopMenu.cs') (Join-Path $projectRoot 'src\L.cs')
if($LASTEXITCODE-ne0){throw 'Context settings test helper failed to compile'}
& $menuControl disable $appExe
Assert-Paper ($LASTEXITCODE-eq0-and-not(Test-Path -LiteralPath $menuRoot)) 'desktop menu setting removes the complete submenu when switched off'
$upgrade=Start-Process -FilePath $installer -ArgumentList '--quiet','--test','--no-launch','--target',('"'+$target+'"') -WindowStyle Hidden -PassThru -Wait
Assert-Paper ($upgrade.ExitCode-eq0-and-not(Test-Path -LiteralPath $menuRoot)) 'upgrade preserves a disabled desktop menu preference'
& $menuControl enable $appExe
Assert-Paper ($LASTEXITCODE-eq0-and(Test-Path -LiteralPath $menuRoot)) 'desktop menu can be enabled again through the shared settings API'
& $menuControl enable $appExe --en
Assert-Paper ((Get-ItemProperty -LiteralPath $menuRoot).MUIVerb -eq 'PaperNest' -and (Get-ItemProperty -LiteralPath (Join-Path $menuVerbs '01_text')).MUIVerb -eq 'New text note') 'desktop menu and creation choices switch to English'
& $menuControl enable $appExe --zh
Assert-Paper ((Get-ItemProperty -LiteralPath $menuRoot).MUIVerb -eq '纸间便签' -and (Get-ItemProperty -LiteralPath (Join-Path $menuVerbs '03_photo')).MUIVerb -eq '新建照片便签') 'desktop menu can switch back to Chinese without changing commands'
& $menuControl wrong-owner $appExe
Assert-Paper ($LASTEXITCODE-eq0-and(Test-Path -LiteralPath $menuRoot)) 'unrelated installation cannot delete the owned menu'
$dataRoot = Join-Path $qaRoot 'Data'
$app = Start-Process -FilePath $appExe -ArgumentList '--data-dir',('"'+$dataRoot+'"') -PassThru
Wait-PaperFile (Join-Path $dataRoot 'notes.json')
$inspection = Read-PaperInspection
Assert-Paper ($inspection.tray -and $inspection.visible -eq 3) 'real app shows three notes and notification-area icon'
Invoke-PaperCommand '--hide'
$inspection = Read-PaperInspection
Assert-Paper ($inspection.visible -eq 0 -and $inspection.tray) 'hide-all leaves tray running with zero visible notes'
Invoke-PaperCommand '--show'
$inspection = Read-PaperInspection
Assert-Paper ($inspection.visible -eq 3) 'second launch restores hidden notes without duplicate instance'
Invoke-PaperCommand '--new'
$inspection = Read-PaperInspection
Assert-Paper ($inspection.notes -eq 4) 'new-note IPC creates exactly one new note'
Invoke-PaperCommand '--hide'
$inspection=Read-PaperInspection
Assert-Paper ($inspection.visible -eq 0) 'all notes can be hidden before closing the app'
Invoke-PaperCommand '--exit'
Assert-Paper ($app.WaitForExit(7000)) 'complete exit removes the app process'
$savedBefore = Get-Content -LiteralPath (Join-Path $dataRoot 'notes.json') -Raw | ConvertFrom-Json
Assert-Paper ($savedBefore.Notes.Count -eq 4) 'complete exit saves the note state'
$app2 = Start-Process -FilePath $appExe -ArgumentList '--new-photo','--data-dir',('"'+$dataRoot+'"') -PassThru
$inspection = Read-PaperInspection
Assert-Paper ($inspection.notes -eq 5 -and $inspection.visible -eq 1) 'photo command starts a closed app and creates exactly one visible photo note'
$typed=Get-Content -LiteralPath (Join-Path $dataRoot 'notes.json') -Raw|ConvertFrom-Json
Assert-Paper ($typed.Notes[-1].Kind-eq'photo') 'cold-start photo command creates the requested note kind'
Invoke-PaperCommand '--new-checklist'
$inspection=Read-PaperInspection
$typed=Get-Content -LiteralPath (Join-Path $dataRoot 'notes.json') -Raw|ConvertFrom-Json
Assert-Paper ($inspection.notes-eq6-and$typed.Notes[-1].Kind-eq'checklist') 'running app receives checklist command without another instance'
Invoke-PaperCommand '--new-text'
$inspection=Read-PaperInspection
$typed=Get-Content -LiteralPath (Join-Path $dataRoot 'notes.json') -Raw|ConvertFrom-Json
Assert-Paper ($inspection.notes-eq7-and$typed.Notes[-1].Kind-eq'text') 'running app receives text command and preserves existing notes'
Invoke-PaperCommand '--show'
$inspection=Read-PaperInspection
Assert-Paper ($inspection.visible-eq7) 'all saved and newly created notes can still be restored'
Invoke-PaperCommand '--exit'
Assert-Paper ($app2.WaitForExit(7000)) 'restarted app exits cleanly'
$uninstallReport = Join-Path $qaRoot 'uninstall.json'
$uninstall = Start-Process -FilePath (Join-Path $target 'Uninstall.exe') -ArgumentList '--uninstall','--quiet','--test','--report',('"'+$uninstallReport+'"') -PassThru -WindowStyle Hidden -Wait
Wait-PaperFile $uninstallReport
Assert-Paper ((Get-Content -LiteralPath $uninstallReport -Raw | ConvertFrom-Json).success) 'uninstaller completes from a separate worker'
Assert-Paper (-not (Test-Path -LiteralPath $appExe)) 'uninstaller removes installed executable'
Assert-Paper (-not (Test-Path -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PaperNest.DesktopNotes.QA')) 'uninstaller removes Windows registration'
Assert-Paper (-not(Test-Path -LiteralPath $menuRoot)-and-not(Test-Path -LiteralPath $menuVerbs)) 'uninstaller removes both owned context menu registrations'
Assert-Paper (Test-Path -LiteralPath (Join-Path $dataRoot 'notes.json')) 'uninstaller preserves note data'
$result = @{ passed = $true; checks = $results; qaRoot = $qaRoot; time = [DateTime]::Now.ToString('s') }
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $projectRoot 'qa\installer-lifecycle.json') -Encoding utf8
$result | ConvertTo-Json -Depth 4

