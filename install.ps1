<#
  ROG 资源管理器主题 — 安装脚本
  · 全部写入 HKCU / %LOCALAPPDATA%，无需管理员权限
  · 安装前自动备份将被修改的注册表项，供卸载还原
#>
[CmdletBinding()]
param(
    [switch]$NoAutostart,      # 不添加开机自启
    [string]$InstallDir = '',  # 安装位置；留空 = %LOCALAPPDATA%\RogHimeTheme（推荐默认）
    [switch]$NoShellExt,       # 不注册资源管理器背景扩展
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$src    = Split-Path -Parent $MyInvocation.MyCommand.Path
$appId  = 'RogHimeTheme'
$name   = 'ROG 资源管理器主题'
$dest   = if ([string]::IsNullOrWhiteSpace($InstallDir)) { Join-Path $env:LOCALAPPDATA $appId } else { $InstallDir }
$cfgFile = Join-Path $dest 'RogHime.ini'
$state  = Join-Path $dest 'state'

function Say([string]$t, [string]$c = 'Gray') { if (-not $Quiet) { Write-Host $t -ForegroundColor $c } }
function Info([string]$t) { Say $t 'Gray' }
function Ok([string]$t)   { Say ('  [OK]   ' + $t) 'Green' }
function Warn([string]$t) { Say ('  [警告] ' + $t) 'Yellow' }

Say ''
Say ('  ' + $name + '  安装程序') 'Red'
Say '  ------------------------------------------------------------'


# ---------------------------------------------------------------- 1. 定位文件
$cands = @($src, (Join-Path $src 'dist'))
$binDir = $null
foreach ($c in $cands) { if (Test-Path (Join-Path $c 'RogHimeEngine.exe')) { $binDir = $c; break } }
if (-not $binDir) {
    Warn '未找到 RogHimeEngine.exe，尝试现场编译…'
    $bp = Join-Path $src 'build.ps1'
    if (Test-Path $bp) { & powershell -NoProfile -ExecutionPolicy Bypass -File $bp | Out-Null }
    if (Test-Path (Join-Path $src 'dist\RogHimeEngine.exe')) { $binDir = Join-Path $src 'dist' }
}
if (-not $binDir) { throw '找不到 RogHimeEngine.exe，请先运行 build.ps1' }
Info ('  程序目录: ' + $binDir)

# ---------------------------------------------------------------- 2. 停止旧实例
Get-Process -Name 'RogHimeEngine' -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_.CloseMainWindow() | Out-Null } catch { }
    Start-Sleep -Milliseconds 200
    try { if (-not $_.HasExited) { $_.Kill() } } catch { }
}
Ok '已停止旧的引擎实例（如果有）'

# ---------------------------------------------------------------- 3. 复制文件
if (-not (Test-Path $dest)) { New-Item -ItemType Directory -Path $dest -Force | Out-Null }
if (-not (Test-Path $state)) { New-Item -ItemType Directory -Path $state -Force | Out-Null }
Copy-Item (Join-Path $binDir 'RogHimeEngine.exe') $dest -Force
if (Test-Path (Join-Path $binDir 'assets'))   { Copy-Item (Join-Path $binDir 'assets')   $dest -Recurse -Force }
if (Test-Path (Join-Path $src 'assets'))      { Copy-Item (Join-Path $src 'assets')      $dest -Recurse -Force }
if (Test-Path (Join-Path $src '使用手册.md')) { Copy-Item (Join-Path $src '使用手册.md') $dest -Force }
if (Test-Path (Join-Path $src 'windhawk'))    { Copy-Item (Join-Path $src 'windhawk')    $dest -Recurse -Force }
if (Test-Path (Join-Path $src 'ExplorerBg')) {
    $bgSrcDir = Join-Path $src 'ExplorerBg'
    $bgDstDir = Join-Path $dest 'ExplorerBg'
    New-Item -ItemType Directory -Path (Join-Path $bgDstDir 'Image') -Force | Out-Null
    foreach ($f in (Get-ChildItem $bgSrcDir -File)) {
        try { Copy-Item $f.FullName (Join-Path $bgDstDir $f.Name) -Force -ErrorAction Stop }
        catch { Warn ($f.Name + ' 正被资源管理器使用，跳过覆盖（已是最新）') }
    }
}
$exe = Join-Path $dest 'RogHimeEngine.exe'
$ini = Join-Path $dest 'RogHime.ini'
if (-not (Test-Path $ini)) {
    & $exe --write-default-config 2>$null | Out-Null
    if (-not (Test-Path $ini)) { Set-Content -Path $ini -Value '# 首次运行会自动生成' -Encoding UTF8 }
}
Ok ('已安装到 ' + $dest)

# ---------------------------------------------------------------- 3.4 资源管理器背景壁纸（外壳扩展）（外壳扩展，首次需要管理员）
$bgDll  = Join-Path $dest 'ExplorerBg\ExplorerBgTool.dll'
$clsid  = '{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}'
$regKey = 'HKLM:\SOFTWARE\Classes\CLSID\' + $clsid + '\InprocServer32'
if ($NoShellExt) {
    Warn '已按安装向导选择跳过资源管理器背景扩展'
} else {
    if (Test-Path $bgDll) {
        $cur = $null
        if (Test-Path $regKey) { $cur = (Get-ItemProperty $regKey -Name '(default)' -ErrorAction SilentlyContinue).'(default)' }
        if ($cur -eq $bgDll) {
            Ok '资源管理器背景壁纸已就绪（可用 资源管理器背景.bat 切换）'
        } else {
            Warn '资源管理器背景壁纸需要管理员权限注册（会弹一次 UAC），完成后自动生效'
            $sw = Join-Path $src 'tools\set-explorer-background.ps1'
            if (Test-Path $sw) {
                try {
                    # 关键：用配置文件里的 BgIndex，而不是写死 1 ✗ 否则第一次打开的窗口显示第 1 张，
                    #       关掉重开才变成配置里那张（外壳扩展会把图片缓存进内存）
                    $bgIdx = 1
                    if (Test-Path $cfgFile) {
                        $m = Select-String -Path $cfgFile -Pattern '^BgIndex\s*=\s*(\d+)'
                        if ($m -and $m.Matches.Count -gt 0) { $bgIdx = [int]$m.Matches[0].Groups[1].Value }
                    }
                    if ($bgIdx -lt 1) { $bgIdx = 1 }
                    Start-Process powershell -Verb RunAs -Wait -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "' + $sw + '" -Index ' + $bgIdx + ' -RestartExplorer') | Out-Null
                    Ok '资源管理器背景壁纸已启用（第 ' + $bgIdx + ' 张）'
                } catch { Warn '背景组件注册被取消，可稍后运行 资源管理器背景.bat 手动启用' }
            }
        }
    }
}

# ---------------------------------------------------------------- 3.5 生成带遮罩的背景图
# 必须在注册外壳扩展【之后】：因为 3.4 会把 assets 里的原图复制过去覆盖，
# 放在它后面才能保证"最终留在磁盘上的是带遮罩的版本" ✗
# （外壳扩展是打开窗口时读图的，只要那时文件已带遮罩就对了）
$exePath = Join-Path $dest 'RogHimeEngine.exe'
if (Test-Path $exePath) {
    try {
        & $exePath --bg-reapply 2>&1 | Out-Null
        Ok '已按配置生成资源管理器背景（含遮罩 / 主文件夹）'
    } catch {
        Warn '背景预生成失败，引擎启动后会自动重做一次'
    }
}

# ---------------------------------------------------------------- 4. 备份注册表
$keys = @{
    'dwm.reg'       = 'HKCU\Software\Microsoft\Windows\DWM'
    'personalize.reg' = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    'run.reg'       = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Run'
    'uninstall.reg' = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall'
    'accent.reg'    = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent'
}
foreach ($k in $keys.GetEnumerator()) {
    $out = Join-Path $state $k.Key
    $exists = $false
    cmd /c ("reg query """ + $k.Value + """ >nul 2>&1") | Out-Null
    if ($LASTEXITCODE -eq 0) { $exists = $true }
    # 只在首次安装时备份，避免重复安装把已改过的值当成原始值存下来
    if ($exists -and -not (Test-Path $out)) { cmd /c ("reg export """ + $k.Value + """ """ + $out + """ /y") | Out-Null }
}
Ok ('已备份注册表到 ' + $state)

# ---------------------------------------------------------------- 6. 系统配色（默认不改！）
# 说明：修改系统强调色会影响【所有程序】的标题栏 / 开关 / 选中框，
#       属于影响面很大的操作，所以默认不写。
#       需要的话可在设置界面勾选「强制把系统强调色改成 ROG 红」，
#       或直接在安装前把 RogHime.ini 里的 EnforceAccent 改成 1。
$wantAccent = $false
if (Test-Path $cfgFile) {
    $wantAccent = [bool](Select-String -Path $cfgFile -Pattern '^EnforceAccent\s*=\s*1' -Quiet)
}
if ($wantAccent) {
    # ROG 红 #FF0029 -> 注册表 ABGR = 0xFF2900FF，作为有符号 int32 即 -14089985
    $accent = -14089985
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\DWM' -Name 'AccentColor' -Value $accent -Type DWord
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\DWM' -Name 'ColorizationColor' -Value $accent -Type DWord
    New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent' -Force | Out-Null
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent' -Name 'AccentColorMenu' -Value $accent -Type DWord
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent' -Name 'StartColorMenu' -Value $accent -Type DWord
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\DWM' -Name 'ColorPrevalence' -Value 1 -Type DWord
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\DWM' -Name 'EnableWindowColorization' -Value 1 -Type DWord
    Ok '红色强调色已应用（按你的配置）'
} else {
    Info '已跳过系统强调色（默认不动系统配色，可在设置里开启）'
}



# ---------------------------------------------------------------- 7. 开机自启
if (-not $NoAutostart) {
    Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $appId -Value ('"' + $exe + '"') -Type String
    Ok '已添加开机自启（可在设置→启动 中关闭）'
}

# ---------------------------------------------------------------- 8. 卸载登记
$unKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $appId
New-Item -Path $unKey -Force | Out-Null
Set-ItemProperty $unKey -Name 'DisplayName'     -Value $name
Set-ItemProperty $unKey -Name 'DisplayVersion'  -Value '2.2.0'
Set-ItemProperty $unKey -Name 'Publisher'       -Value 'RogHime Theme'
Set-ItemProperty $unKey -Name 'InstallLocation' -Value $dest
Set-ItemProperty $unKey -Name 'UninstallString' -Value ('powershell -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $dest 'uninstall.ps1') + '"')
Set-ItemProperty $unKey -Name 'NoModify'        -Value 1 -Type DWord
Set-ItemProperty $unKey -Name 'NoRepair'        -Value 1 -Type DWord
Copy-Item (Join-Path $src 'uninstall.ps1') $dest -Force
Ok '已登记到「设置 → 应用」'

# ---------------------------------------------------------------- 9. 启动
Start-Process -FilePath $exe -WorkingDirectory $dest
Start-Sleep -Milliseconds 800
if (Get-Process -Name 'RogHimeEngine' -ErrorAction SilentlyContinue) { Ok 'ROG 引擎已启动（托盘图标）' }
else { Warn '引擎未能启动，请手动运行 ' + $exe }

Say '  ------------------------------------------------------------'
Say '  安装完成！打开任意资源管理器窗口即可看到效果。' 'Red'
Say ('  配置文件: ' + $ini)
Say ('  卸载: 运行 ' + (Join-Path $src '卸载.bat') + '  或 设置→应用→' + $name)
Say ''
