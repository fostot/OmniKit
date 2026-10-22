[CmdletBinding()]
param([string]$Revision = '0.1.0-dev.2')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'src/OmniKit/OmniKit.csproj') -Raw
$expectedBaseVersion = [string]$project.Project.PropertyGroup.Version
$expectedAssemblyVersion = [string]$project.Project.PropertyGroup.AssemblyVersion
$expectedFileVersion = [string]$project.Project.PropertyGroup.FileVersion
$expectedRevision = [string]$project.Project.PropertyGroup.InformationalVersion
if ($expectedBaseVersion -notmatch '^\d+\.\d+\.\d+$' -or
    $Revision -notmatch ('^' + [regex]::Escape($expectedBaseVersion) + '-(dev|test|rc)\.[0-9]+$') -or
    $Revision -cne $expectedRevision) { throw 'Requested revision must match the project prerelease version.' }
if ($expectedAssemblyVersion -notmatch '^\d+\.\d+\.\d+\.\d+$' -or
    ([version]$expectedAssemblyVersion).ToString(3) -cne $expectedBaseVersion -or
    $expectedFileVersion -cne $expectedAssemblyVersion) { throw 'Project assembly/file versions must use the expected base and numeric build revision.' }

$releasePath = Join-Path $projectRoot 'src/OmniKit/bin/Release'
$umbraPath = Join-Path $projectRoot 'src/OmniKit.Umbra/bin/Release/OmniKit.Umbra.dll'
$stagingPath = Join-Path $projectRoot ('.local/package-' + [guid]::NewGuid().ToString('N'))
$distributionPath = Join-Path $projectRoot ('dist/' + $Revision)
if (Test-Path -LiteralPath $distributionPath) { throw 'This distribution revision already exists. Preserve it and package a new internal revision.' }

# An explicit runtime allowlist avoids shipping reference libraries, settings,
# PDBs, developer output, unrelated runtimes or a prior package recursively.
$runtimeFiles = @(
    'OmniKit.dll', 'OmniKit.json', 'OmniKit.deps.json',
    'Microsoft.Data.Sqlite.dll', 'SQLitePCLRaw.batteries_v2.dll',
    'SQLitePCLRaw.core.dll', 'SQLitePCLRaw.provider.e_sqlite3.dll', 'e_sqlite3.dll',
    'System.Security.Cryptography.ProtectedData.dll', 'System.Speech.dll',
    'assets/bimble.png', 'assets/moonbear.png', 'assets/companions-walk.png'
)
foreach ($relative in $runtimeFiles) {
    $source = Join-Path $releasePath $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing build file: $relative" }
}
if (-not (Test-Path -LiteralPath $umbraPath -PathType Leaf)) { throw 'Missing Umbra companion build.' }
$manifest = Get-Content -LiteralPath (Join-Path $releasePath 'OmniKit.json') -Raw | ConvertFrom-Json
$manifestAssemblyVersion = [string]$manifest.AssemblyVersion
if ($manifestAssemblyVersion -cne $expectedAssemblyVersion -or $manifest.DalamudApiLevel -ne 15) { throw 'Manifest assembly version or API level does not match the expected build.' }
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $releasePath 'OmniKit.dll')).Version.ToString()
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $releasePath 'OmniKit.dll'))
if ($assemblyVersion -cne $expectedAssemblyVersion -or $info.FileVersion -cne $expectedFileVersion) { throw 'DLL assembly/file versions do not match the expected build.' }
if ($info.ProductVersion -cne $Revision -and -not $info.ProductVersion.StartsWith($Revision + '+', [StringComparison]::Ordinal)) { throw 'Build informational version does not match requested revision.' }

New-Item -ItemType Directory -Path $stagingPath, $distributionPath | Out-Null
foreach ($relative in $runtimeFiles) {
    $source = Join-Path $releasePath $relative
    $destination = Join-Path $stagingPath $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $stagingPath
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/THIRD-PARTY-NOTICES.md') -Destination $stagingPath
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $stagingPath
Compress-Archive -Path (Join-Path $stagingPath '*') -DestinationPath (Join-Path $distributionPath 'OmniKit.zip')
Copy-Item -LiteralPath $umbraPath -Destination (Join-Path $distributionPath 'OmniKit.Umbra.dll')

$download = "https://github.com/fostot/OmniKit/releases/download/v$Revision/OmniKit.zip"
$feed = [ordered]@{
    Author = 'OmniKit contributors'; Name = 'OmniKit'; InternalName = 'OmniKit'
    AssemblyVersion = $manifestAssemblyVersion; DalamudApiLevel = 15; ApplicableVersion = 'any'
    Punchline = 'Your everyday toolkit for XIV.'
    Description = "Early preview $Revision. Compact search, recorded keyboard chords, styled target HUD, walking moon companions, gathering gear, voice shortcuts and Umbra integration. Game validation is still pending."
    RepoUrl = 'https://github.com/fostot/OmniKit'
    Tags = @('search', 'utility', 'accessibility', 'umbra')
    DownloadLinkInstall = $download; DownloadLinkUpdate = $download; DownloadLinkTesting = $download
    IconUrl = 'https://raw.githubusercontent.com/fostot/OmniKit/main/assets/bimble.png'
    LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    IsHide = $false; IsTestingExclusive = $false
}
ConvertTo-Json -InputObject @($feed) -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'repo.json') -Encoding utf8NoBOM
$hashes = @('OmniKit.zip', 'OmniKit.Umbra.dll') | ForEach-Object {
    $hash = Get-FileHash -LiteralPath (Join-Path $distributionPath $_) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + $_
}
$hashes | Set-Content -LiteralPath (Join-Path $distributionPath 'SHA256SUMS.txt') -Encoding utf8NoBOM
Write-Output "Packaged $Revision. Review dist/$Revision and repo.json before publishing."
