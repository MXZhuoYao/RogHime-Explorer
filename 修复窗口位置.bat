@echo off
echo 正在把跑到屏幕外的资源管理器窗口拉回可视区域...
powershell -NoProfile -Command "$e = Join-Path $env:LOCALAPPDATA 'RogHimeTheme\RogHimeEngine.exe'; & $e --fit-windows; Start-Sleep -Seconds 2"
pause
