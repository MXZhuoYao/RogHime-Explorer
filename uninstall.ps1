<#
  ROG 资源管理器主题 — 卸载脚本
  还原安装前的注册表与壁纸，删除程序文件。
#>
[CmdletBinding()]
param([switch]$KeepFiles, [switch]$Quiet)

$ErrorActionPreference = 'Continue'
$src   = Split-Path -Parent $MyInvocation.MyCommand.Path
$appId = 'RogHimeTheme'
$name  = 'ROG 资源管理器主题'
$dest  = Join-Path $env:LOCALAPPDATA $appId
$state = Join-Path $dest 'state'

function Say([string]$t, [string]$c = 'Gray') { if (-not $Quiet) { Write-Host $t -ForegroundColor $c } }

Say ''
Say ('  ' + $name + '  卸载程序') 'Red'
Say '  ------------------------------------------------------------'

# 1. 停止引擎
Get-Process -Name 'RogHimeEngine' -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_.Kill() } catch { }
}
Start-Sleep -Milliseconds 300
Say '  [OK]   已停止引擎' 'Green'

# 1.5 反注册资源管理器背景外壳扩展（先做，否则 DLL 被占用删不掉）
$clsid  = '{ED15A97D-FE3E-4CDE-98FF-BC46B02896B0}'
$regKey = 'HKLM:\SOFTWARE\Classes\CLSID\' + $clsid + '\InprocServer32'
$folderKey = 'HKLM:\SOFTWARE\Classes\Drive\shellex\FolderExtensions\' + $clsid
if (Test-Path $regKey) {
    $cur = (Get-ItemProperty $regKey -Name '(default)' -ErrorAction SilentlyContinue).'(default)'
    if ($cur -like '*RogHimeTheme*') {
        $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        try {
            if ($isAdmin) { Start-Process regsvr32.exe -ArgumentList ('/u /s "' + $cur + '"') -Wait }
            else { Start-Process regsvr32.exe -Verb RunAs -ArgumentList ('/u /s "' + $cur + '"') -Wait }
        } catch { }
        Remove-Item $folderKey -Recurse -Force -ErrorAction SilentlyContinue
        # 重启资源管理器，释放已加载的 DLL
        Get-Process explorer -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 2
        if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe }
        Start-Sleep -Seconds 2
        Say '  [OK]   已卸载资源管理器背景壁纸组件' 'Green'
    }
}

# 2. 移除自启
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $appId -ErrorAction SilentlyContinue
Say '  [OK]   已移除开机自启' 'Green'

# 3. 还原注册表
foreach ($f in @('dwm.reg','personalize.reg','accent.reg')) {
# （不含 desktop.reg：本主题从不修改桌面壁纸）
    $p = Join-Path $state $f
    if (Test-Path $p) { cmd /c ('reg import "' + $p + '" >nul 2>&1') | Out-Null; Say ('  [OK]   已还原 ' + $f) 'Green' }
}
# Run 与 Uninstall 单独处理（避免还原出无关项）
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name $appId -ErrorAction SilentlyContinue
Remove-Item ('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $appId) -Recurse -Force -ErrorAction SilentlyContinue
Say '  [OK]   已移除卸载登记' 'Green'

# 4. 桌面壁纸：本主题不再接管壁纸（安装时也不修改），此处不做任何操作。

# 5. 删除文件
if (-not $KeepFiles) {
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force -ErrorAction SilentlyContinue }
    Say ('  [OK]   已删除 ' + $dest) 'Green'
}
Say '  ------------------------------------------------------------'
Say '  卸载完成。资源管理器已恢复原样。' 'Red'
Say ''

# ---------------------------------------------------------------- Windhawk 残留检测
Write-Host ""
Write-Host "[检测] Windhawk 里是否还有 ROG 相关 mod ..." -ForegroundColor Cyan
try {
    $whRoot = 'HKLM:\SOFTWARE\Windhawk\Engine\Mods'
    if (Test-Path $whRoot) {
        $left = Get-ChildItem $whRoot -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match 'roghime' }
        if ($left) {
            Write-Host "  [!] 发现以下 mod 仍在 Windhawk 中（可能导致资源管理器背景残留）：" -ForegroundColor Yellow
            foreach ($m in $left) {
                $dis = (Get-ItemProperty $m.PSPath -Name Disabled -ErrorAction SilentlyContinue).Disabled
                Write-Host ("      - " + $m.PSChildName + $(if ($dis -eq 1) { "  （已停用）" } else { "  （★仍在启用）" })) -ForegroundColor Yellow
            }
            Write-Host "  → 请打开 Windhawk，把上面这些 mod 停用或卸载后，再重启资源管理器。" -ForegroundColor Yellow
            Write-Host "  → Windhawk 没有命令行卸载 mod 的接口，这一步必须手动完成。" -ForegroundColor DarkGray
        } else {
            Write-Host "  [OK] Windhawk 中没有 RO姬 相关 mod" -ForegroundColor Green
        }
    } else {
        Write-Host "  [OK] 未安装 Windhawk（或没有 mod 记录）" -ForegroundColor Green
    }
} catch {
    Write-Host "  [跳过] Windhawk 检测失败：$($_.Exception.Message)" -ForegroundColor DarkGray
}
