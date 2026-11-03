param([switch]$Package)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$buildDir = Join-Path $projectRoot 'build\app'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$frameworkDir = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $frameworkDir 'csc.exe'
$references = @('System.dll','System.Core.dll','System.Xml.dll','System.Xaml.dll','System.Runtime.Serialization.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir ('WPF\' + $_)) }
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$appExe = Join-Path $buildDir 'PaperNest.exe'
$compileArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+',('/out:'+$appExe),('/win32manifest:'+(Join-Path $projectRoot 'app.manifest'))) + $references
if (Test-Path -LiteralPath (Join-Path $buildDir 'PaperNest.ico')) { $compileArgs += '/win32icon:'+(Join-Path $buildDir 'PaperNest.ico') }
& $compiler @compileArgs @sourceFiles
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'PaperNest.exe.config') -Destination $buildDir -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\Fonts') -Destination $buildDir -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $buildDir 'Assets') -Force | Out-Null
if (Test-Path -LiteralPath (Join-Path $projectRoot 'assets\Sunset.png')) { Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\Sunset.png') -Destination (Join-Path $buildDir 'Assets\Sunset.png') -Force }
$iconProcess = Start-Process -FilePath $appExe -ArgumentList '--write-icon', ('"'+(Join-Path $buildDir 'PaperNest.ico')+'"') -WindowStyle Hidden -PassThru -Wait
if ($iconProcess.ExitCode -ne 0) { throw 'Icon generation failed' }
if (-not ($compileArgs | Where-Object { $_ -like '/win32icon:*' })) {
    $compileArgs += '/win32icon:'+(Join-Path $buildDir 'PaperNest.ico')
    & $compiler @compileArgs @sourceFiles
    if ($LASTEXITCODE -ne 0) { throw 'Application icon embedding failed' }
}
foreach ($notice in @('LICENSE','THIRD_PARTY_NOTICES.md')) {
    if (Test-Path -LiteralPath (Join-Path $projectRoot $notice)) { Copy-Item -LiteralPath (Join-Path $projectRoot $notice) -Destination $buildDir -Force }
}
if ($Package) { & (Join-Path $projectRoot 'package.ps1') }
