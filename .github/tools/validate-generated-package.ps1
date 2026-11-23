param([string] $Version = '0.4.0-generation-local')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $repoRoot
try {
    $runRoot = Join-Path $repoRoot ('artifacts/generation-package-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runRoot | Out-Null
    & dotnet pack src/TelegramBotKit/TelegramBotKit.csproj -c Release --no-restore -o $runRoot "-p:PackageVersion=$Version" *> (Join-Path $runRoot 'pack.log')
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $runRoot/pack.log" }
    & dotnet pack src/TelegramBotKit.Routing/TelegramBotKit.Routing.csproj -c Release --no-build -o $runRoot "-p:PackageVersion=$Version" *> (Join-Path $runRoot 'pack-routing.log')
    if ($LASTEXITCODE -ne 0) { throw "Routing pack failed: $runRoot/pack-routing.log" }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $package = [IO.Compression.ZipFile]::OpenRead((Join-Path $runRoot "TelegramBotKit.$Version.nupkg"))
    try {
        if (-not $package.GetEntry('analyzers/dotnet/cs/TelegramBotKit.Generators.dll')) { throw 'Bundled generator is missing.' }
        foreach ($framework in @('net8.0', 'net9.0', 'net10.0', 'net11.0')) {
            if (-not $package.GetEntry("lib/$framework/TelegramBotKit.dll")) { throw "Core assembly is missing for $framework." }
        }
        $entry = $package.GetEntry('TelegramBotKit.nuspec')
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $forbidden = $spec.SelectNodes('//*[local-name()="dependency"]') | Where-Object {
            $_.id -eq 'TelegramBotKit.Generators' -or $_.id -like 'Microsoft.CodeAnalysis*'
        }
        if ($forbidden) { throw 'Compiler assemblies must not be runtime package dependencies.' }
    } finally { $package.Dispose() }
    $consumer = 'tests/Packaging/Consumer/Consumer.csproj'
    $cache = Join-Path $runRoot 'packages'
    $globalCache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else {
        $profileRoot = if ($env:USERPROFILE) { $env:USERPROFILE } else { [Environment]::GetFolderPath('UserProfile') }
        Join-Path $profileRoot '.nuget/packages'
    }
    foreach ($transitive in @('false', 'true')) {
        $restoreLog = Join-Path $runRoot "restore-$transitive.log"
        & dotnet restore $consumer --source $runRoot --source $globalCache --packages $cache "-p:TelegramBotKitValidationVersion=$Version" "-p:UseTransitiveCore=$transitive" -p:NuGetAudit=false *> $restoreLog
        if ($LASTEXITCODE -ne 0) { throw "Consumer restore failed: $restoreLog" }
        foreach ($framework in @('net8.0', 'net9.0', 'net10.0', 'net11.0')) {
            $consumerLog = Join-Path $runRoot "consumer-$framework-$transitive.log"
            & dotnet run --project $consumer -c Release --framework $framework --no-restore "-p:TelegramBotKitValidationVersion=$Version" "-p:UseTransitiveCore=$transitive" *> $consumerLog
            if ($LASTEXITCODE -ne 0) { throw "Consumer failed: $consumerLog" }
            if (-not (Select-String -LiteralPath $consumerLog -SimpleMatch 'PACKED_GENERATION_OK')) { throw 'Consumer did not finish validation.' }
            $deps = Get-Content -LiteralPath "tests/Packaging/Consumer/bin/Release/$framework/Consumer.deps.json" -Raw | ConvertFrom-Json
            if ($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -match 'Microsoft.CodeAnalysis|TelegramBotKit.Generators' }) {
                throw "Compiler dependencies leaked into the $framework consumer runtime graph."
            }
        }
    }
    Write-Host "PACKED_GENERATION_OK: $runRoot"
} finally { Pop-Location }
