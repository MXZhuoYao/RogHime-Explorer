<#
  ROG 资源管理器主题 — 资源管理器背景壁纸（可切换 / 可关闭）
  · 底层使用 Maplespe 的 ExplorerBgTool（MIT）外壳扩展，把图片画在文件列表背景上
  · 通过本脚本切换壁纸 / 调整显示方式，改完只需重开一个资源管理器窗口即可生效
  · 需要管理员权限（注册/反注册外壳扩展），脚本会自动提权
#>
[CmdletBinding()]
param(
    [int]$Index = -1,            # 0=关闭 1/2/3=第 N 张壁纸
    [int]$PosType = 6,           # 0左上 1右上 2左下 3右下 4居中 5缩放 6缩放并填充
    [int]$Alpha = 255,           # 0-255 不透明度
    [switch]$Interactive,
    [switch]$RestartExplorer
)

$ErrorActionPreference = 'Stop'
$rootDir = Join-Path $env:LOCALAPPDATA 'RogHimeTheme'
$bgDir   = Join-Path $rootDir 'ExplorerBg'
$dll     = Join-Path $bgDir 'ExplorerBgTool.dll'
$imgDir  = Join-Path $bgDir 'Image'
$cfg     = Join-Path $bgDir 'config.ini'
$assets  = Join-Path $rootDir 'assets\wallpapers'
$clsid   = '{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}'

function Say([string]$t, [string]$c = 'Gray') { Write-Host $t -ForegroundColor $c }
function Pause2 { if ($Interactive) { Write-Host ''; Read-Host '按回车键退出' | Out-Null } }

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $line = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath + '" -Index ' + $Index + ' -PosType ' + $PosType + ' -Alpha ' + $Alpha
    if ($Interactive) { $line += ' -Interactive' }
    if ($RestartExplorer) { $line += ' -RestartExplorer' }
    Say '需要管理员权限，正在提权…' 'Yellow'
    Start-Process powershell -Verb RunAs -ArgumentList $line | Out-Null
    exit
}

Say ''
Say '  ROG 资源管理器主题 — 资源管理器背景壁纸' 'Red'
Say '  ------------------------------------------------------------'

if (-not (Test-Path $dll)) { Say ('  找不到背景组件：' + $dll + '（请先运行 安装.bat）') 'Red'; Pause2; exit 1 }

function Get-RegisteredPath {
    $k = 'HKLM:\SOFTWARE\Classes\CLSID\' + $clsid + '\InprocServer32'
    if (Test-Path $k) { return (Get-ItemProperty $k -Name '(default)' -ErrorAction SilentlyContinue).'(default)' }
    return $null
}
function Register-Ext {
    $p = Start-Process regsvr32.exe -ArgumentList ('/s "' + $dll + '"') -Verb RunAs -PassThru -Wait
    return ($p.ExitCode -eq 0)
}
function Unregister-Ext {
    if (Get-RegisteredPath) { Start-Process regsvr32.exe -ArgumentList ('/u /s "' + $dll + '"') -Verb RunAs -PassThru -Wait | Out-Null }
}
function Restart-ExplorerNow {
    Say '  正在重启资源管理器以加载/卸载组件…' 'Yellow'
    Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
    Start-Sleep -Seconds 3
}

# ---------------------------------------------------------------- 选择壁纸
if ($Index -lt 0) {
    Write-Host ''
    Write-Host '   1) ROG 机械红 战姬'
    Write-Host '   2) RO姬 立绘 + ROG 标志'
    Write-Host '   3) ROG 霓虹字标'
    Write-Host '   0) 关闭（恢复系统默认背景）'
    Write-Host ''
    $Index = [int](Read-Host '请输入 1 / 2 / 3 / 0')
}

# ---------------------------------------------------------------- 关闭
if ($Index -eq 0) {
    Unregister-Ext
    if (Get-RegisteredPath) { Say '  反注册失败，请手动运行 regsvr32 /u' 'Red'; Pause2; exit 1 }
    Say '  已关闭资源管理器背景' 'Green'
    Restart-ExplorerNow
    Say '  完成。' 'Green'
    Say '  ------------------------------------------------------------'
    Pause2; exit
}

# ---------------------------------------------------------------- 布置图片
New-Item -ItemType Directory -Path $imgDir -Force | Out-Null
Get-ChildItem $imgDir -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
$src = Join-Path $assets ('bg' + $Index + '.jpg')
if (-not (Test-Path $src)) { Say ('  找不到壁纸：' + $src) 'Red'; Pause2; exit 1 }
$dst = Join-Path $imgDir ('bg' + $Index + '.jpg')
Copy-Item $src $dst -Force

# ---------------------------------------------------------------- 写配置
if (-not (Test-Path $cfg)) { Say ('  找不到配置：' + $cfg) 'Red'; Pause2; exit 1 }
$t = Get-Content $cfg -Raw -Encoding Default
$t = [regex]::Replace($t, '(?m)^random=.*$',   'random=false')
$t = [regex]::Replace($t, '(?m)^custom=.*$',   'custom=false')
$t = [regex]::Replace($t, '(?m)^posType=.*$',   ('posType=' + $PosType))
$t = [regex]::Replace($t, '(?m)^imgAlpha=.*$',  ('imgAlpha=' + $Alpha))
$t = [regex]::Replace($t, '(?m)^folder=.*$',    'folder=')
Set-Content -Path $cfg -Value $t -Encoding Default -NoNewline

# ---------------------------------------------------------------- 确保已注册
$reg = Get-RegisteredPath
if ($reg -ne $dll) {
    if ($reg) { Unregister-Ext }
    if (Register-Ext) { Say '  背景组件已注册' 'Green'; Restart-ExplorerNow }
    else { Say '  注册失败（regsvr32 返回错误）' 'Red'; Pause2; exit 1 }
}

Say ('  已选择壁纸：bg' + $Index + '.jpg   （显示方式 posType=' + $PosType + '，不透明度 ' + $Alpha + '/255）') 'Green'
Say '  重开一个资源管理器窗口即可看到效果（无需重启系统）。' 'Gray'
if ($RestartExplorer) { Restart-ExplorerNow }
Say '  ------------------------------------------------------------'
Pause2
