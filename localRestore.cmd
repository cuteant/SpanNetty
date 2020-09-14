@if not defined _echo @echo off
setlocal enabledelayedexpansion

SET CMDHOME=%~dp0.
if "%BUILD_FLAGS%"=="" SET BUILD_FLAGS=/m /v:m
if not defined BuildConfiguration SET BuildConfiguration=Debug

:: Clear the 'Platform' env variable for this session, as it's a per-project setting within the build, and
:: misleading value (such as 'MCD' in HP PCs) may lead to build breakage (issue: #69).
set Platform=

:: Disable multilevel lookup https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/multilevel-sharedfx-lookup.md
set DOTNET_MULTILEVEL_LOOKUP=0 

call Ensure-DotNetSdk.cmd

SET SOLUTION=%CMDHOME%\DotNetty.sln

@echo ===== Restoring %SOLUTION% =====

@echo Restore %BuildConfiguration% ==============================
SET STEP=Restore %BuildConfiguration%

call %_dotnet% restore %BUILD_FLAGS% /bl:%BuildConfiguration%-Restore.binlog /p:Configuration=%BuildConfiguration%%AdditionalConfigurationProperties% "%SOLUTION%"
@if ERRORLEVEL 1 GOTO :ErrorStop
@echo RESTORE ok for %BuildConfiguration% %SOLUTION%


:BuildFinished
@echo ===== Restore succeeded for %SOLUTION% =====
@GOTO :EOF

:ErrorStop
set RC=%ERRORLEVEL%
if "%STEP%" == "" set STEP=%BuildConfiguration%
@echo ===== Restore FAILED for %SOLUTION% -- %STEP% with error %RC% - CANNOT CONTINUE =====
exit /B %RC%
:EOF
