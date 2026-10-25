$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dependency = Get-Content (Join-Path $PSScriptRoot 'dependencies.json') -Raw | ConvertFrom-Json
$cache = Join-Path $root '.build-cache'
New-Item -ItemType Directory -Force -Path $cache | Out-Null
$archive = Join-Path $cache 'ffmpeg.zip'
if (-not (Test-Path $archive)) { Invoke-WebRequest -Uri $dependency.url -OutFile $archive }
$actual = (Get-FileHash $archive -Algorithm SHA256).Hash
if ($actual -ne $dependency.sha256) { throw 'FFmpeg 下载校验失败；上游可能已更新版本。请核验新版本后更新 dependencies.json，或使用之前已校验的归档。' }
$extracted = Join-Path $cache 'ffmpeg'
Expand-Archive -LiteralPath $archive -DestinationPath $extracted -Force
$binary = @(Get-ChildItem -LiteralPath $extracted -Recurse -Filter 'ffmpeg.exe')
if ($binary.Count -ne 1) { throw 'FFmpeg 归档结构不符合预期' }
$folder = Split-Path (Split-Path $binary[0].FullName -Parent) -Parent
New-Item -ItemType Directory -Force -Path (Join-Path $root 'bin') | Out-Null
Copy-Item -LiteralPath $binary[0].FullName -Destination (Join-Path $root 'bin/ffmpeg.exe') -Force
Copy-Item -LiteralPath (Join-Path $folder 'LICENSE') -Destination (Join-Path $root 'bin/FFmpeg-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $folder 'README.txt') -Destination (Join-Path $root 'bin/FFmpeg-BUILD.txt') -Force
