@echo off
rem Builds the seeded-crash programs into tests\seeded\bin. Needs the MSVC compiler (cl) on PATH,
rem for example from a "Developer Command Prompt" or vcvars64.bat. Run only on throwaway machines.
setlocal
set SRC=%~dp0src
set OUT=%~dp0bin
if not exist "%OUT%" mkdir "%OUT%"
pushd "%OUT%"

rem /Od: no optimization, so the crashes happen exactly as written. /MT: static runtime, no DLL needed.
cl /nologo /Od /MT /LD "%SRC%\seed_module.c" /Fe:SherlogSeedModule.dll || goto :fail
cl /nologo /Od /MT "%SRC%\seed_crash_in_module.c" /Fe:SherlogSeedCrash.exe || goto :fail
cl /nologo /Od /MT "%SRC%\seed_stack_overflow.c" /Fe:SherlogSeedOverflow.exe || goto :fail
cl /nologo /Od /MT "%SRC%\seed_fastfail.c" /Fe:SherlogSeedFastFail.exe || goto :fail
rem /MD: shared runtime on purpose (sandbox case).
cl /nologo /Od /MD "%SRC%\seed_needs_runtime.c" /Fe:SherlogSeedNeedsRuntime.exe || goto :fail

del /q *.obj *.exp *.lib 2>nul
popd
echo Built seeded programs in %OUT%
exit /b 0

:fail
popd
echo Build failed.
exit /b 1
