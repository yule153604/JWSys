@echo off
chcp 65001 >nul
echo ==============================================
echo      JWSystem - 教务系统自动化工具
echo ==============================================
echo.

REM Get script directory
set SCRIPT_DIR=%~dp0

REM Check for published or development environment
if exist "%SCRIPT_DIR%JWSystem.exe" (
    REM Published directory
    echo [INFO] 检测到发布版本，直接运行程序...
    echo.
    cd /d "%SCRIPT_DIR%"
    call .\JWSystem.exe
) else (
    REM Development directory
    echo [INFO] 检测到开发环境，使用 'dotnet run' 运行...
    set PROJECT_DIR=%~dp0\JWSystem

    REM Check if .NET is installed
    dotnet --version >nul 2>&1
    if %errorlevel% neq 0 (
        echo [ERROR] 未找到 .NET SDK，请先安装 .NET 8.0 SDK
        echo 下载地址: https://dotnet.microsoft.com/zh-cn/download
        echo.
        pause
        exit /b 1
    )

    echo [INFO] 检测到 .NET SDK 版本:
    dotnet --version
    echo.
    echo [INFO] 检查配置文件...
    if not exist "%PROJECT_DIR%\appsettings.json" (
        echo [WARNING] 未找到 appsettings.json 配置文件
        echo [INFO] 请参考 README.md 配置文件说明进行设置
    ) else (
        echo [OK] 找到配置文件
    )

    echo.
    echo [INFO] 进入项目目录...
    cd /d "%PROJECT_DIR%"
    if %errorlevel% neq 0 (
        echo [ERROR] 无法进入项目目录 %PROJECT_DIR%
        pause
        exit /b 1
    )

    echo.
    echo [INFO] 还原 NuGet 包...
    dotnet restore
    if %errorlevel% neq 0 (
        echo [ERROR] NuGet 包还原失败
        pause
        exit /b 1
    )

    echo.
    echo [INFO] 编译项目...
    dotnet build
    if %errorlevel% neq 0 (
        echo [ERROR] 项目编译失败
        pause
        exit /b 1
    )

    echo.
    echo [SUCCESS] 编译成功！启动程序...
    echo.
    dotnet run
)

echo.
echo [INFO] 程序执行完毕
pause