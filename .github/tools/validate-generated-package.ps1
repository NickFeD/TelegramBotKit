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
        if (-not $package.GetEntry('lib/net10.0/TelegramBotKit.dll')) { throw 'Core assembly is missing.' }
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
    & dotnet restore $consumer --source $runRoot --source $globalCache --packages $cache "-p:TelegramBotKitValidationVersion=$Version" -p:NuGetAudit=false *> (Join-Path $runRoot 'restore.log')
    if ($LASTEXITCODE -ne 0) { throw "Consumer restore failed: $runRoot/restore.log" }
    & dotnet run --project $consumer -c Release --no-restore "-p:TelegramBotKitValidationVersion=$Version" *> (Join-Path $runRoot 'consumer.log')
    if ($LASTEXITCODE -ne 0) { throw "Consumer failed: $runRoot/consumer.log" }
    if (-not (Select-String -LiteralPath (Join-Path $runRoot 'consumer.log') -SimpleMatch 'PACKED_GENERATION_OK')) { throw 'Consumer did not finish validation.' }
    $deps = Get-Content -LiteralPath 'tests/Packaging/Consumer/bin/Release/net10.0/Consumer.deps.json' -Raw | ConvertFrom-Json
    if ($deps.libraries.PSObject.Properties.Name | Where-Object { $_ -match 'Microsoft.CodeAnalysis|TelegramBotKit.Generators' }) {
        throw 'Compiler dependencies leaked into the consumer runtime graph.'
    }
    & dotnet restore $consumer --source $runRoot --source $globalCache --packages $cache "-p:TelegramBotKitValidationVersion=$Version" -p:UseTransitiveCore=true -p:NuGetAudit=false *> (Join-Path $runRoot 'transitive-restore.log')
    if ($LASTEXITCODE -ne 0) { throw "Transitive restore failed: $runRoot/transitive-restore.log" }
    & dotnet run --project $consumer -c Release --no-restore "-p:TelegramBotKitValidationVersion=$Version" -p:UseTransitiveCore=true *> (Join-Path $runRoot 'transitive-consumer.log')
    if ($LASTEXITCODE -ne 0) { throw "Transitive consumer failed: $runRoot/transitive-consumer.log" }
    if (-not (Select-String -LiteralPath (Join-Path $runRoot 'transitive-consumer.log') -SimpleMatch 'PACKED_GENERATION_OK')) { throw 'Transitive consumer did not finish validation.' }
    Write-Host "PACKED_GENERATION_OK: $runRoot"
} finally { Pop-Location }
