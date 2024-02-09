@if not defined _echo @echo off
setlocal enabledelayedexpansion

SET CMDHOME=%~dp0.
if "%BUILD_FLAGS%"=="" SET BUILD_FLAGS=/m /v:m
if not defined BuildConfiguration SET BuildConfiguration=Release
if not defined PublishConfiguration SET PublishConfiguration=dev

:: Clear the 'Platform' env variable for this session, as it's a per-project setting within the build, and
:: misleading value (such as 'MCD' in HP PCs) may lead to build breakage (issue: #69).
set Platform=

:: Disable multilevel lookup https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/multilevel-sharedfx-lookup.md
set DOTNET_MULTILEVEL_LOOKUP=0 

call Ensure-DotNetSdk.cmd

SET SOLUTION=%CMDHOME%\DotNetty.CrossPlatform.sln

:: Set DateTime prefix or suffix for builds
if "%PublishConfiguration%" == "dev" for /f %%j in ('powershell -NoProfile -ExecutionPolicy ByPass Get-Date -format "{yyMMdd}"') do set DATE_SUFFIX=%%j
if "%PublishConfiguration%" == "dev" SET AdditionalConfigurationProperties=;VersionDateSuffix=%DATE_SUFFIX%
if "%PublishConfiguration%" == "release" for /f %%j in ('powershell -NoProfile -ExecutionPolicy ByPass Get-Date -format "{yyMM}"') do set YEAR_PREFIX=%%j
if "%PublishConfiguration%" == "release" for /f %%j in ('powershell -NoProfile -ExecutionPolicy ByPass Get-Date -format "{ddHH}"') do set DATE_PREFIX=%%j
if "%PublishConfiguration%" == "release" SET AdditionalConfigurationProperties=;VersionYearPrefix=%YEAR_PREFIX%;VersionDatePrefix=%DATE_PREFIX%

@echo ===== Building %SOLUTION% =====

@echo Build %BuildConfiguration% ==============================
SET STEP=Restore %BuildConfiguration%

call %_dotnet% restore %BUILD_FLAGS% /bl:%BuildConfiguration%-Restore.binlog /p:Configuration=%BuildConfiguration%%AdditionalConfigurationProperties% "%SOLUTION%"
@if ERRORLEVEL 1 GOTO :ErrorStop
@echo RESTORE ok for %BuildConfiguration% %SOLUTION%

SET STEP=Build %BuildConfiguration%
call %_dotnet% build --no-restore %BUILD_FLAGS% /bl:%BuildConfiguration%-Build.binlog /p:IncludeSymbols=true /p:SymbolPackageFormat=snupkg /p:Configuration=%BuildConfiguration%%AdditionalConfigurationProperties% "%SOLUTION%"
@if ERRORLEVEL 1 GOTO :ErrorStop
@echo BUILD ok for %BuildConfiguration% %SOLUTION%


:BuildFinished
@echo ===== Build succeeded for %SOLUTION% =====
@GOTO :EOF

:ErrorStop
set RC=%ERRORLEVEL%
if "%STEP%" == "" set STEP=%BuildConfiguration%
@echo ===== Build FAILED for %SOLUTION% -- %STEP% with error %RC% - CANNOT CONTINUE =====
exit /B %RC%
:EOF
