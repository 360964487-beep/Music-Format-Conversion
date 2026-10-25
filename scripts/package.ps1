$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION 格式无效' }
& (Join-Path $root 'src/build.ps1')
if (-not (Test-Path (Join-Path $root 'bin/ffmpeg.exe'))) { & (Join-Path $PSScriptRoot 'get-ffmpeg.ps1') }
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist ('QingZhuan-'+$version+'-'+[guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage '轻转'
New-Item -ItemType Directory -Force -Path $payload | Out-Null
foreach ($item in @('轻转.exe','src','bin','使用说明.txt','README.md','LICENSE','THIRD_PARTY_NOTICES.md','VERSION')) {
 Copy-Item -LiteralPath (Join-Path $root $item) -Destination $payload -Recurse -Force
}
$zip = Join-Path $dist ('QingZhuan-'+$version+'-windows-x64.zip')
Compress-Archive -LiteralPath $payload -DestinationPath $zip -CompressionLevel Optimal -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Value ($hash+'  '+(Split-Path $zip -Leaf)) -Encoding ascii
Write-Output $zip
