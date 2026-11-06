$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Windows.Forms.dll','Microsoft.CSharp.dll')|ForEach-Object{'/r:'+(Join-Path $framework $_)}
$refs+=@('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll')|ForEach-Object{'/r:'+(Join-Path "$framework\WPF" $_)}
& "$framework\csc.exe" /nologo /target:winexe /optimize+ ('/win32icon:'+(Join-Path $PSScriptRoot 'app.ico')) ('/out:'+(Join-Path $root '轻转.exe')) @refs (Join-Path $PSScriptRoot 'App.cs') (Join-Path $PSScriptRoot 'Preferences.cs') (Join-Path $PSScriptRoot 'VideoDownload.cs') (Join-Path $PSScriptRoot 'VideoWindow.cs') (Join-Path $PSScriptRoot 'DesktopShortcut.cs') (Join-Path $PSScriptRoot 'SingleInstance.cs')
if($LASTEXITCODE -ne 0){throw '编译失败'}
