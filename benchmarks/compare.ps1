param(
    [Parameter(Mandatory = $true)] [string] $Baseline,
    [Parameter(Mandatory = $true)] [string] $Candidate
)

$ErrorActionPreference = 'Stop'
function Read-Measurements([string] $path) {
    $cases = @{}
    $reports = @(Get-ChildItem -LiteralPath $path -Recurse -File -Filter '*-report-full.json')
    if ($reports.Count -eq 0) { throw "No full JSON reports in $path" }
    foreach ($report in $reports) {
        $data = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
        foreach ($case in $data.Benchmarks) {
            if ($case.DisplayInfo -match ': Dry\(' -or $null -eq $case.Statistics) {
                throw "Dry or failed measurement is not comparable: $($case.FullName)"
            }
            if ($cases.ContainsKey($case.FullName)) {
                throw "Multiple measurements/jobs for $($case.FullName); select one run/job directory."
            }
            $cases[$case.FullName] = $case
        }
    }
    return $cases
}

$before = Read-Measurements $Baseline
$after = Read-Measurements $Candidate
foreach ($name in ($before.Keys | Sort-Object)) {
    if (-not $after.ContainsKey($name)) { Write-Warning "Missing candidate: $name"; continue }
    $old = $before[$name]
    $new = $after[$name]
    if ($old.Statistics.Mean -le 0) { Write-Warning "Baseline is at timing noise floor: $name"; continue }
    $ratio = $new.Statistics.Mean / $old.Statistics.Mean
    [pscustomobject][ordered]@{
        Benchmark = $name
        BaselineNs = [math]::Round($old.Statistics.Mean, 3)
        CandidateNs = [math]::Round($new.Statistics.Mean, 3)
        TimeRatio = [math]::Round($ratio, 3)
        TimeChangePercent = [math]::Round(($ratio - 1) * 100, 2)
        BaselineBytes = $old.Memory.BytesAllocatedPerOperation
        CandidateBytes = $new.Memory.BytesAllocatedPerOperation
        BaselineSamples = $old.Statistics.N
        CandidateSamples = $new.Statistics.N
        BaselineErrorNs = $old.Statistics.ConfidenceInterval.Margin
        CandidateErrorNs = $new.Statistics.ConfidenceInterval.Margin
    }
}
foreach ($name in $after.Keys) {
    if (-not $before.ContainsKey($name)) { Write-Warning "New candidate case has no baseline: $name" }
}
