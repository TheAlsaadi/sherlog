<#
.SYNOPSIS
Runs Sherlog's seeded crashes inside Windows Sandbox and scores them (Gate 1: 100% of seeded crashes matched).

.DESCRIPTION
1. Publishes sherlog.exe (self-contained) and copies the seeded programs into sandbox\.work\app.
2. Starts Windows Sandbox with that folder mapped read-only and an output folder mapped read-write.
   Inside, sandbox\inside\Run-Inside.ps1 crashes the programs, runs `sherlog collect`, and shuts the sandbox down.
3. Puts the known causes (tests\seeded\expected.yaml) into the bundle and runs `sherlog eval`.

Nothing runs on this PC except publishing and evaluating; the crashes happen only in the sandbox.
Needs Windows Sandbox (Windows 11 Pro or higher, feature enabled). No admin rights needed.

.PARAMETER ProgramsDir
Folder with the seeded programs built by CI (artifact "seeded-programs"). Download one with:
  gh run download <run-id> --name seeded-programs --dir sandbox\.work\programs
#>
param(
    [string] $ProgramsDir = (Join-Path $PSScriptRoot '.work\programs'),
    [int] $TimeoutMinutes = 15
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$work = Join-Path $PSScriptRoot '.work'
$app = Join-Path $work 'app'
$out = Join-Path $work 'out'

foreach ($program in 'SherlogSeedCrash.exe', 'SherlogSeedModule.dll', 'SherlogSeedOverflow.exe', 'SherlogSeedFastFail.exe', 'SherlogSeedNeedsRuntime.exe') {
    if (-not (Test-Path (Join-Path $ProgramsDir $program))) { throw "Missing $program in $ProgramsDir. See the help: Get-Help $PSCommandPath" }
}
if (-not (Test-Path "$env:windir\System32\WindowsSandbox.exe")) { throw 'Windows Sandbox is not installed on this PC.' }
if (Get-Process WindowsSandbox, WindowsSandboxClient -ErrorAction SilentlyContinue) { throw 'Windows Sandbox is already running; close it first.' }

Remove-Item $app, $out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $app, $out, (Join-Path $app 'bin') | Out-Null

Write-Host 'Publishing sherlog.exe...'
dotnet publish (Join-Path $repo 'src\Sherlog.Cli') -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o (Join-Path $app 'sherlog') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
Copy-Item (Join-Path $ProgramsDir '*') (Join-Path $app 'bin')
Copy-Item (Join-Path $PSScriptRoot 'inside\Run-Inside.ps1') $app

$wsb = Join-Path $work 'seeded.wsb'
@"
<Configuration>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <AudioInput>Disable</AudioInput>
  <VideoInput>Disable</VideoInput>
  <PrinterRedirection>Disable</PrinterRedirection>
  <MappedFolders>
    <MappedFolder><HostFolder>$app</HostFolder><SandboxFolder>C:\seeded</SandboxFolder><ReadOnly>true</ReadOnly></MappedFolder>
    <MappedFolder><HostFolder>$out</HostFolder><SandboxFolder>C:\out</SandboxFolder><ReadOnly>false</ReadOnly></MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\seeded\Run-Inside.ps1</Command>
  </LogonCommand>
</Configuration>
"@ | Set-Content -Path $wsb -Encoding utf8

Write-Host 'Starting Windows Sandbox. It closes itself when the run is done.'
Start-Process "$env:windir\System32\WindowsSandbox.exe" -ArgumentList "`"$wsb`""
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while (-not (Test-Path (Join-Path $out 'done.txt'))) {
    if ((Get-Date) -gt $deadline) { throw "The sandbox run did not finish within $TimeoutMinutes minutes. See $out." }
    Start-Sleep -Seconds 10
}

$bundle = Join-Path $out 'seeded.zip'
if (-not (Test-Path $bundle)) { throw "No bundle was collected. See $(Join-Path $out 'inside-log.txt')." }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($bundle, 'Update')
try {
    $zip.GetEntry('label.yaml').Delete()
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $repo 'tests\seeded\expected.yaml'), 'label.yaml') | Out-Null
}
finally { $zip.Dispose() }

$sherlog = Join-Path $app 'sherlog\sherlog.exe'
$libraries = @('--library', (Join-Path $repo 'library\recipes'), '--library', (Join-Path $repo 'tests\seeded\recipes'))
& $sherlog eval $out @libraries
$report = (& $sherlog eval $out @libraries --json) -join "`n" | ConvertFrom-Json
Write-Host ''
Write-Host "Results are in $out (bundle, inside-log.txt, event-summary.txt, runtime-events.txt)."
if ($report.seeded.labeled -eq 3 -and $report.seeded.correct -eq 3) {
    Write-Host 'Seeded crashes: 3 of 3 matched. Gate line passes.'
    exit 0
}
Write-Host "Seeded crashes: $($report.seeded.correct) of $($report.seeded.labeled) matched. Gate line fails."
exit 1
