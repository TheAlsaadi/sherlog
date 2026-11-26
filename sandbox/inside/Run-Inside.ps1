# Runs INSIDE Windows Sandbox (a throwaway Windows). Crashes the seeded programs on purpose, then collects a Sherlog
# evidence bundle into the mapped output folder and shuts the sandbox down. Never run this on a real PC.
$ErrorActionPreference = 'Continue'
$out = 'C:\out'
$bin = 'C:\seeded\bin'
Start-Transcript -Path (Join-Path $out 'inside-log.txt') | Out-Null
$started = Get-Date

function Invoke-Seeded([string] $program, [int] $timeoutSeconds) {
    $path = Join-Path $bin $program
    try {
        $process = Start-Process -FilePath $path -WorkingDirectory $bin -PassThru -WindowStyle Hidden
        if (-not $process.WaitForExit($timeoutSeconds * 1000)) {
            $process.Kill()
            Write-Output "$program did not exit within $timeoutSeconds s; killed it."
        }
        else {
            Write-Output ("{0} exited with 0x{1:x8}" -f $program, $process.ExitCode)
        }
    }
    catch {
        Write-Output "$program could not start: $($_.Exception.Message)"
    }
}

# The three crashes Gate 1 checks.
Invoke-Seeded 'SherlogSeedCrash.exe' 60
Invoke-Seeded 'SherlogSeedOverflow.exe' 60
Invoke-Seeded 'SherlogSeedFastFail.exe' 60

# Discovery: what does Windows record when the Visual C++ runtime is missing? (The sandbox has no runtime installed.)
Invoke-Seeded 'SherlogSeedNeedsRuntime.exe' 20

Start-Sleep -Seconds 30 # Windows Error Reporting writes its records after the process ends.

# Counts of what was logged during the run, by provider and event ID (sandbox data only).
$summary = foreach ($log in 'Application', 'System') {
    Get-WinEvent -FilterHashtable @{ LogName = $log; StartTime = $started } -ErrorAction SilentlyContinue |
        Group-Object ProviderName, Id | Sort-Object Count -Descending |
        ForEach-Object { "{0,-12} {1,4}  {2}" -f $log, $_.Count, $_.Name }
}
$summary | Set-Content -Path (Join-Path $out 'event-summary.txt') -Encoding utf8

# Full text of the few events about the runtime program, so a recipe can be written from them.
Get-WinEvent -FilterHashtable @{ LogName = 'System', 'Application'; StartTime = $started } -ErrorAction SilentlyContinue |
    Where-Object { $_.Message -match 'SherlogSeedNeedsRuntime|VCRUNTIME|MSVCP' } |
    ForEach-Object { "----- $($_.LogName) / $($_.ProviderName) / $($_.Id)`n$($_.Message)`n$($_.ToXml())" } |
    Set-Content -Path (Join-Path $out 'runtime-events.txt') -Encoding utf8

& 'C:\seeded\sherlog\sherlog.exe' collect --no-questions --days 1 --out (Join-Path $out 'seeded.zip')
Write-Output "collect exit code: $LASTEXITCODE"

Stop-Transcript | Out-Null
Set-Content -Path (Join-Path $out 'done.txt') -Value (Get-Date -Format o)
shutdown.exe /s /t 5
