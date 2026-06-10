@echo off
chcp 65001
setlocal EnableDelayedExpansion

REM ===============================
REM 配置路径
REM ===============================
set WORKSPACE=.
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_FILE=.\Gen.conf

set CLIENT_DATA_DIR=..\Assets\Res\Data\Configs
set CLIENT_CODE_DIR=..\Assets\Scripts\Game\Data\Runtime\Configs

REM ===============================
REM 菜单上次选择（仅本次运行有效，不落地文件）
if not defined LAST_CHOICE set "LAST_CHOICE=1"

:MENU
cls
echo ---------------------------------
echo  数据导出工具
echo ---------------------------------
echo  1. 导出客户端 (client)
echo  2. 退出
echo ---------------------------------
set /p choice=请输入选项(1-2) [回车默认 %LAST_CHOICE%]:

REM ======== 默认值处理 ==========
if "%choice%"=="" set "choice=%LAST_CHOICE%"
if "%choice%"=="1" (set "LAST_CHOICE=1" & goto EXPORT_CLIENT)
if "%choice%"=="2" goto END
echo 选项无效，请重新输入...
pause
goto MENU

REM ===============================
:EXPORT_ALL
echo.
call :DO_EXPORT_SERVER
echo.
call :DO_EXPORT_CLIENT
goto DONE
REM ===============================


REM ===============================
:EXPORT_SERVER
echo.
call :DO_EXPORT_SERVER
goto DONE
REM ===============================


REM ===============================
:EXPORT_CLIENT
echo.
call :DO_EXPORT_CLIENT
goto DONE
REM ===============================


REM ===============================
:DONE
echo.
echo 导出完成，请按任意键返回菜单...
pause
goto MENU
REM ===============================


REM ===============================
:SVN_UPDATE
echo.
echo === SVN 更新开始 ===
svn update %WORKSPACE%
echo === SVN 更新完成 ===
echo.
pause
goto MENU

:SVN_CLEANUP
echo.
echo === SVN 清理开始 ===
svn cleanup %WORKSPACE%
echo === SVN 清理完成 ===
echo.
pause
goto MENU

:DO_EXPORT_SERVER
echo === 导出服务器 ===
dotnet %LUBAN_DLL% ^
-t server ^
-c lua-lua ^
-d lua ^
--conf %CONF_FILE% ^
-x outputCodeDir=%SERVER_CODE_DIR% ^
-x outputDataDir=%SERVER_DATA_DIR%
exit /b %errorlevel%

:DO_EXPORT_CLIENT
echo === 导出客户端 ===
dotnet %LUBAN_DLL% ^
    -t all ^
    -c cs-simple-json ^
    -d json ^
    --conf %CONF_FILE% ^
    -x outputDataDir=%CLIENT_DATA_DIR% ^
    -x outputCodeDir=%CLIENT_CODE_DIR%
exit /b %errorlevel%
REM ===============================


REM ===============================
:END
echo 退出工具...
endlocal
pause