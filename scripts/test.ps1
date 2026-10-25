$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& "$root\src\build.ps1"
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll') | ForEach-Object {'/r:'+(Join-Path $framework $_)}
$refs += @('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll') | ForEach-Object {'/r:'+(Join-Path "$framework\WPF" $_)}
$refs += '/r:'+(Join-Path $root '轻转.exe')
& "$framework\csc.exe" /nologo /target:exe ('/out:'+(Join-Path $root 'Verify.exe')) @refs (Join-Path $root 'tests\Verify.cs')
if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
& "$root\Verify.exe" $root
if ($LASTEXITCODE -ne 0) { throw '测试失败' }
