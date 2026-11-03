$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$buildDir = Join-Path $projectRoot 'build\app'
$payload = Join-Path $projectRoot 'build\Payload.zip'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload }
& (Join-Path $projectRoot 'build.ps1')
$payloadStream = [IO.File]::Create($payload)
$archive = [IO.Compression.ZipArchive]::new($payloadStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $buildDir -File -Recurse | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object {
        $relative = $_.FullName.Substring($buildDir.Length + 1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $payloadStream.Dispose() }
$frameworkDir = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Drawing.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkDir ('WPF\' + $_)) }
$installer = Join-Path $projectRoot 'build\PaperNest-Setup.exe'
$setupCompileArgs = @('/nologo','/target:winexe','/platform:anycpu','/optimize+',('/out:'+$installer),('/win32manifest:'+(Join-Path $projectRoot 'app.manifest')),('/win32icon:'+(Join-Path $buildDir 'PaperNest.ico')),('/resource:'+$payload+',Payload.zip')) + $references
& (Join-Path $frameworkDir 'csc.exe') @setupCompileArgs (Join-Path $projectRoot 'Installer.cs') (Join-Path $projectRoot 'src\Artwork.cs') (Join-Path $projectRoot 'src\AppIcon.cs') (Join-Path $projectRoot 'src\DesktopMenu.cs') (Join-Path $projectRoot 'src\WindowsHost.cs') (Join-Path $projectRoot 'src\L.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'PaperNest.exe.config') -Destination ($installer + '.config') -Force
Get-Item -LiteralPath $installer | Select-Object FullName, Length
