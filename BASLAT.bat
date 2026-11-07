@echo off
setlocal
chcp 65001 >nul
title Arena Dovusu - Baslatici
cd /d "%~dp0"

rem ---- Node.js: once projedeki tasinabilir surum, yoksa bilgisayardaki
set "NODE=%~dp0tools\node\node.exe"
set "NPM=%~dp0tools\node\npm.cmd"
if not exist "%NODE%" (
  where node >nul 2>nul && (set "NODE=node" & set "NPM=npm") || (
    echo [HATA] Node.js bulunamadi. tools\node klasorunu indirin ya da https://nodejs.org adresinden kurun.
    pause
    exit /b 1
  )
)

rem ---- Kopru bagimliliklari (ilk calistirmada bir kez)
set "ARENA_INSTALL=0"
if not exist "bridge\node_modules\ws" set "ARENA_INSTALL=1"
if not exist "bridge\node_modules\tiktok-live-connector" set "ARENA_INSTALL=1"
if "%ARENA_INSTALL%"=="1" (
  echo Kopru paketleri kuruluyor...
  pushd bridge
  if "%NPM%"=="npm" (
    call npm install --no-audit --no-fund
  ) else (
    call "%NPM%" install --no-audit --no-fund
  )
  if errorlevel 1 (
    popd
    echo [HATA] Kopru paketleri kurulamadi.
    pause
    exit /b 1
  )
  popd
)

rem ---- 1) Kopru (TikTok / simulator -> oyun)
"%NODE%" bridge\src\ensure-running.js
if errorlevel 1 (
  pause
  exit /b 1
)

rem ---- 2) Yayinci paneli (tarayicida)
start "" "http://localhost:8788"

rem ---- 3) Oyun penceresi (608x1080, cercevesiz, ekranin sag kenarinda)
if exist "Builds\Windows\ArenaDovusu.exe" (
  start "" "Builds\Windows\ArenaDovusu.exe" -popupwindow
) else (
  echo [UYARI] Oyun henuz derlenmemis: Builds\Windows\ArenaDovusu.exe yok.
  echo Unity'de Arena ^> Windows icin derle menusunu calistirin.
  pause
)
