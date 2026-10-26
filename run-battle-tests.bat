@echo off
setlocal enabledelayedexpansion

echo ========================================
echo MicroMediator Battle Testing Suite
echo ========================================
echo.

REM Run BenchmarkDotNet suite
echo [1/5] Running BenchmarkDotNet benchmarks...
echo ========================================
cd src\TechnicalDogsbody.MicroMediator\TechnicalDogsbody.MicroMediator.Benchmarks

dotnet run -c Release --filter * > ..\..\..\test-results\benchmark-results.txt 2>&1

if errorlevel 1 (
    echo ERROR: Benchmarks failed. Check test-results\benchmark-results.txt
    exit /b 1
)

echo DONE - Results in test-results\benchmark-results.txt
echo.

REM Run unit tests
echo [2/5] Running unit tests...
echo ========================================
cd ..\TechnicalDogsbody.MicroMediator.Tests

dotnet test -c Release --logger "console;verbosity=minimal" > ..\..\..\test-results\unit-test-results.txt 2>&1

if errorlevel 1 (
    echo ERROR: Unit tests failed. Check test-results\unit-test-results.txt
    exit /b 1
)

echo DONE - All tests passed
echo.

REM Check if K6 is installed
where k6 >nul 2>&1
if errorlevel 1 (
    echo [3/5] SKIPPING K6 tests - K6 not installed
    echo Install: choco install k6
    echo.
    goto skip_k6
)

REM Start API in background
echo [3/5] Starting API for K6 load tests...
echo ========================================
cd ..\TechnicalDogsbody.MicroMediator.Examples

start /B dotnet run -c Release > ..\..\..\test-results\api-logs.txt 2>&1
set API_PID=%ERRORLEVEL%

REM Wait for API to start
timeout /t 10 /nobreak > nul

REM Test if API is responding
curl -s http://localhost:5000/api/products/1 > nul 2>&1
if errorlevel 1 (
    echo ERROR: API failed to start. Check test-results\api-logs.txt
    taskkill /F /IM dotnet.exe 2>nul
    exit /b 1
)

echo API started successfully
echo.

REM Run K6 tests
echo [4/5] Running K6 load tests...
echo ========================================
cd ..\..\..\k6-load-tests

echo Running stress test...
k6 run stress-test.js --summary-export=..\test-results\k6-stress-summary.json > ..\test-results\k6-stress-results.txt 2>&1

echo Running cache performance test...
k6 run cache-performance-test.js --summary-export=..\test-results\k6-cache-summary.json > ..\test-results\k6-cache-results.txt 2>&1

echo Running spike test...
k6 run spike-test.js --summary-export=..\test-results\k6-spike-summary.json > ..\test-results\k6-spike-results.txt 2>&1

REM Stop API
taskkill /F /IM dotnet.exe 2>nul

echo DONE - Results in test-results\k6-*.txt
echo.

:skip_k6

REM Memory analysis reminder
echo [5/5] Manual verification needed
echo ========================================
echo.
echo Next steps for complete battle testing:
echo.
echo 1. Review benchmark results in test-results\
echo 2. Check for memory leaks:
echo    - Look for Gen2 GC increases in MemoryLeakBenchmarks
echo    - Verify stable memory in streaming tests
echo.
echo 3. If available, run with profiler:
echo    dotnet-counters monitor --process-id [PID]
echo    dotMemory or PerfView for detailed analysis
echo.
echo 4. Deploy to test environment:
echo    - Azure Functions (test cold starts)
echo    - Kubernetes (test under autoscaling)
echo    - Docker (test with limited resources)
echo.

echo ========================================
echo Battle testing complete!
echo ========================================
echo.
echo Review results in test-results\ directory
echo.

endlocal
