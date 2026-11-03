param([string]$Output = (Join-Path $PSScriptRoot 'docs\images'))
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$framework='C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$appDir=Join-Path $PSScriptRoot 'build\app'
$exe=Join-Path $appDir 'RenderGallery.exe'
$references=@('System.dll','System.Core.dll','System.Xaml.dll')|ForEach-Object{'/reference:'+(Join-Path $framework $_)}
$references+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')|ForEach-Object{'/reference:'+(Join-Path $framework ('WPF\'+$_))}
$references+='/reference:'+(Join-Path $appDir 'PaperNest.exe')
& (Join-Path $framework 'csc.exe') /nologo /target:winexe ('/out:'+$exe) @references (Join-Path $PSScriptRoot 'tests\RenderGallery.cs')
if($LASTEXITCODE-ne0){throw 'Gallery compilation failed'}
$data=Join-Path $PSScriptRoot ('qa\gallery-'+[guid]::NewGuid().ToString('N'))
$process=Start-Process -FilePath $exe -ArgumentList ('"'+$Output+'"'),('"'+$data+'"') -WindowStyle Hidden -PassThru -Wait
if($process.ExitCode-ne0){throw 'Gallery render failed'}
# The rendering helper is not part of the shipped app.
Remove-Item -LiteralPath $exe
