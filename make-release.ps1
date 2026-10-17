<#
  ROG 资源管理器主题 — 发布打包
#>
[CmdletBinding()]
param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$root  = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist  = Join-Path $root 'dist'
$rel   = Join-Path $root 'release'
$name  = 'RogHime-Explorer-v2.2.0'
$stage = Join-Path $rel $name

if (-not (Test-Path $dist)) { throw 'dist 不存在，请先运行 build.ps1' }
if (-not (Test-Path $rel)) { New-Item -ItemType Directory -Path $rel -Force | Out-Null }
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage -Force | Out-Null

# 打包后清理：排除不参与运行的素材目录（壁纸原图等）
$items = @('RogHimeEngine.exe','install.ps1','uninstall.ps1','使用手册.md','README.md','安装.bat','卸载.bat','修复窗口位置.bat','资源管理器背景.bat','RogHime.ini','assets','windhawk','tools','ExplorerBg')
foreach ($i in $items) { $p = Join-Path $dist $i; if (Test-Path $p) { Copy-Item $p $stage -Recurse -Force; Write-Host ('  + ' + $i) } }

$zip = Join-Path $rel ($name + '-win64.zip')
if (Test-Path $zip) { Remove-Item $zip -Force }
Get-ChildItem (Join-Path $stage 'assets\wallpapers\_source') -Recurse -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $stage 'assets\wallpapers\_source') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $stage 'report.txt') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $stage 'RogHime.log') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $stage 'state') -Recurse -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$h = (Get-FileHash $zip -Algorithm SHA256).Hash
Set-Content -Path (Join-Path $rel 'SHA256SUMS.txt') -Value ($h + '  ' + (Split-Path $zip -Leaf)) -Encoding ASCII
Write-Host ''
Write-Host ('发布目录: ' + $stage)
Write-Host ('压缩包  : ' + $zip)
Write-Host ('SHA256  : ' + $h)
