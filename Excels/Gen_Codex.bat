@echo off
chcp 65001 > nul
setlocal EnableExtensions

rem Non-interactive export script for command-line/Codex usage.
rem It can be launched from any working directory.
pushd "%~dp0" > nul

set "WORKSPACE=%CD%"
set "LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll"
set "CONF_FILE=%WORKSPACE%\Gen.conf"

set "CLIENT_DATA_DIR=%WORKSPACE%\..\Assets\Res\Data\Configs"
set "CLIENT_CODE_DIR=%WORKSPACE%\..\Assets\Scripts\Game\Data\Runtime\Configs"

if not exist "%LUBAN_DLL%" (
    echo Luban.dll not found: "%LUBAN_DLL%"
    popd > nul
    exit /b 1
)

if not exist "%CONF_FILE%" (
    echo Gen.conf not found: "%CONF_FILE%"
    popd > nul
    exit /b 1
)

echo === Export client configs ===
dotnet "%LUBAN_DLL%" ^
    -t all ^
    -c cs-simple-json ^
    -d json ^
    --conf "%CONF_FILE%" ^
    -x outputDataDir="%CLIENT_DATA_DIR%" ^
    -x outputCodeDir="%CLIENT_CODE_DIR%"

set "EXIT_CODE=%ERRORLEVEL%"
if "%EXIT_CODE%"=="0" (
    echo === Export completed ===
) else (
    echo === Export failed, code %EXIT_CODE% ===
)

popd > nul
exit /b %EXIT_CODE%
