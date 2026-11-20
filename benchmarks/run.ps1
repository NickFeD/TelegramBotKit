param(
    [ValidateSet('Validate', 'Short', 'Full', 'Load')]
    [string] $Mode = 'Full',
    [string] $Filter = '*',
    [ValidateRange(100, 10000)]
    [int] $IterationTimeMs = 500
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $runRoot = Join-Path $repoRoot "BenchmarkDotNet.Artifacts/$stamp-$Mode"
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    $project = 'benchmarks/TelegramBotKit.Benchmarks/TelegramBotKit.Benchmarks.csproj'
    & dotnet build $project -c Release -v quiet *> (Join-Path $runRoot 'build.log')
    if ($LASTEXITCODE -ne 0) { throw "Build failed; see $runRoot/build.log" }

    & dotnet --info | Out-File (Join-Path $runRoot 'dotnet-info.txt') -Encoding utf8
    if ($env:OS -eq 'Windows_NT') {
        Get-ItemProperty -LiteralPath 'HKLM:\HARDWARE\DESCRIPTION\System\CentralProcessor\0' |
            Select-Object ProcessorNameString | Out-File (Join-Path $runRoot 'cpu.txt') -Encoding utf8
        & powercfg /getactivescheme | Out-File (Join-Path $runRoot 'power-plan.txt') -Encoding utf8
    }
    & git status --short | Out-File (Join-Path $runRoot 'git-status.txt') -Encoding utf8
    $files = @(& rg --files src benchmarks/TelegramBotKit.Benchmarks -g '*.cs' -g '*.csproj' -g 'packages.lock.json')
    $files += 'Directory.Build.props', 'global.json', 'benchmarks/run.ps1'
    $manifest = foreach ($file in ($files | Sort-Object -Unique)) {
        # BDN searches for the project by filename; snapshots must not look like projects.
        $destination = Join-Path $runRoot "source/$file.snapshot"
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file -Destination $destination
        [ordered]@{ path = $file; sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
    }
    $benchmarkArgs = @('--filter', $Filter, '--noOverwrite', '--artifacts', (Join-Path $runRoot 'measurements'))
    if ($Mode -eq 'Validate') { $benchmarkArgs += '--job', 'Dry' }
    if ($Mode -eq 'Short') { $benchmarkArgs += '--job', 'Short' }
    if ($Mode -eq 'Full') { $benchmarkArgs += '--iterationTime', $IterationTimeMs.ToString() }
    if ($Mode -eq 'Load') { $benchmarkArgs = @('--load') }
    [ordered]@{
        started = (Get-Date).ToString('o'); head = (& git rev-parse HEAD)
        mode = $Mode; arguments = $benchmarkArgs; files = @($manifest)
    } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $runRoot 'manifest.json') -Encoding utf8

    Write-Host "Running $Mode; output: $runRoot/run.log"
    # Windows PowerShell wraps native stderr as ErrorRecord (e.g. WMI information warnings).
    # Let the benchmark's checked exit status decide success, retaining stderr in the log.
    $ErrorActionPreference = 'Continue'
    & dotnet run --project $project -c Release --no-build -- @benchmarkArgs *> (Join-Path $runRoot 'run.log')
    $benchmarkExitCode = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($benchmarkExitCode -ne 0) { throw "Benchmark failed; see $runRoot/run.log" }
    Write-Host "Completed: $runRoot"
} finally {
    Pop-Location
}
