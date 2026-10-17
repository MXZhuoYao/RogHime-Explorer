<#
  RO姬资源管理器主题 - 构建脚本
  只依赖 Windows 自带的 C# 编译器（%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe）
  无需 Visual Studio / .NET SDK / NuGet / 联网。
#>
[CmdletBinding()]
param([switch]$Clean)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src\Engine'
$dist = Join-Path $root 'dist'
$log  = Join-Path $root 'build.log'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe not found' }

$systemDrawing = Join-Path (Split-Path $csc) 'System.Drawing.dll'
$systemWinForms = Join-Path (Split-Path $csc) 'System.Windows.Forms.dll'
foreach ($r in @($systemDrawing, $systemWinForms)) { if (-not (Test-Path $r)) { throw ('missing ref: ' + $r) } }

if ($Clean -and (Test-Path $dist)) { Remove-Item $dist -Recurse -Force }
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }

$script:lines = New-Object System.Collections.Generic.List[string]
function Say([string]$t) { $script:lines.Add($t); Write-Host $t }

function Build([string]$name, [string[]]$files, [string]$target, [string[]]$switches) {
    $out = Join-Path $dist ($name + '.exe')
    if (Test-Path $out) { Remove-Item $out -Force }
    $a = @('/nologo','/platform:x64','/optimize+','/warn:4','/preferreduilang:en-US','/langversion:5',
           ('/out:' + $out), ('/target:' + $target),
           ('/r:' + $systemDrawing), ('/r:' + $systemWinForms)) + $switches
    $a += $files
    Say ('--- ' + $name + '.exe')
    $res = & $csc @a 2>&1
    foreach ($l in $res) { Say ('    ' + [string]$l) }
    if (-not (Test-Path $out)) { throw ('BUILD FAILED: ' + $name) }
}

$manifest = Join-Path $src 'app.manifest'
$files = @(Get-ChildItem $src -Filter '*.cs' | ForEach-Object { $_.FullName })
if ($files.Count -eq 0) { throw 'no source files' }

$icon = Join-Path $root 'assets\RogHime.ico'
$sw = @(('/win32manifest:' + $manifest))
if (Test-Path $icon) { $sw += ('/win32icon:' + $icon) } else { Write-Host ('WARNING: icon not found at ' + $icon) }
Build 'RogHimeEngine' $files 'winexe' $sw

# 把素材与脚本一起放进 dist，使 dist 成为可直接运行的完整包
$assets = Join-Path $root 'assets'
if (Test-Path $assets) {
    $assetsDst = Join-Path $dist 'assets'
    if (Test-Path $assetsDst) { Remove-Item $assetsDst -Recurse -Force }   # 防止已删除的旧素材残留在 dist
    Copy-Item $assets $dist -Recurse -Force
}
$bgSrc = Join-Path $root 'ExplorerBg'
if (Test-Path $bgSrc) {
    $bgDst = Join-Path $dist 'ExplorerBg'
    if (Test-Path $bgDst) { Remove-Item $bgDst -Recurse -Force }
    Copy-Item $bgSrc $dist -Recurse -Force
    $imgDst = Join-Path $bgDst 'Image'
    if (Test-Path $imgDst) { Get-ChildItem $imgDst -File | Remove-Item -Force }
}
foreach ($h in @('install.ps1','uninstall.ps1','使用手册.md','卸载.bat','安装.bat','修复窗口位置.bat','README.md','VERSION.txt','资源管理器背景.bat')) {
    $p = Join-Path $root $h
    if (Test-Path $p) { Copy-Item $p $dist -Force }
}
$toolsSrc = Join-Path $root 'tools'
if (Test-Path $toolsSrc) { $toolsDst = Join-Path $dist 'tools'; if (-not (Test-Path $toolsDst)) { New-Item -ItemType Directory -Path $toolsDst | Out-Null }; Get-ChildItem $toolsSrc -File | Copy-Item -Destination $toolsDst -Force }
$whd = Join-Path $root 'windhawk'
if (Test-Path $whd) { Copy-Item $whd $dist -Recurse -Force }

Say '--- dist ---'
foreach ($f in (Get-ChildItem $dist)) { Say ('    ' + $f.Name + '  ' + $f.Length + ' bytes') }
[System.IO.File]::WriteAllLines($log, $script:lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Host 'BUILD OK'
