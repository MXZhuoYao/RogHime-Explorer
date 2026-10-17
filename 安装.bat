@echo off
title RO姬 资源管理器主题 · 安装
cd /d "%~dp0"

rem ── 自提权：不是管理员就重新以管理员身份启动自己（只弹一次 UAC）
net session >nul 2>&1
if %errorlevel% neq 0 (
  echo   正在请求管理员权限...
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

echo.
echo   RO姬 资源管理器主题  ·  安装程序
echo   ------------------------------------------------------------
echo   安装位置 : %LOCALAPPDATA%\RogHimeTheme
echo   开机自启 : 注册表启动项（可在 设置 - 高级 里改成其它方式）
echo   卸载方式 : 运行 卸载.bat，或在 设置 - 应用 里卸载
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
echo.
echo   按任意键关闭...
pause >nul
