[CmdletBinding()]
param([string]$Revision = '0.1.0-dev.1')
$ErrorActionPreference = 'Stop'
if ($Revision -notmatch '^0\.1\.0-(dev|test|rc)\.[0-9]+$') { throw 'Use the current 0.1.0 prerelease version scheme.' }
$projectRoot = Split-Path -Parent $PSScriptRoot
$releasePath = Join-Path $projectRoot 'src/OmniKit/bin/Release'
$umbraPath = Join-Path $projectRoot 'src/OmniKit.Umbra/bin/Release/OmniKit.Umbra.dll'
$stagingPath = Join-Path $projectRoot ('.local/package-' + [guid]::NewGuid().ToString('N'))
$distributionPath = Join-Path $projectRoot ('dist/' + $Revision)
New-Item -ItemType Directory -Path $stagingPath, $distributionPath -Force | Out-Null

# An explicit runtime allowlist avoids shipping reference libraries, settings,
# PDBs, developer output, unrelated runtimes or a prior package recursively.
$runtimeFiles = @(
    'OmniKit.dll', 'OmniKit.json', 'OmniKit.deps.json',
    'Microsoft.Data.Sqlite.dll', 'SQLitePCLRaw.batteries_v2.dll',
    'SQLitePCLRaw.core.dll', 'SQLitePCLRaw.provider.e_sqlite3.dll', 'e_sqlite3.dll',
    'System.Security.Cryptography.ProtectedData.dll', 'System.Speech.dll',
    'assets/bimble.png', 'assets/moonbear.png'
)
foreach ($relative in $runtimeFiles) {
    $source = Join-Path $releasePath $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing build file: $relative" }
    $destination = Join-Path $stagingPath $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
}
$manifest = Get-Content -LiteralPath (Join-Path $stagingPath 'OmniKit.json') -Raw | ConvertFrom-Json
if ($manifest.AssemblyVersion -ne '0.1.0.0' -or $manifest.DalamudApiLevel -ne 15) { throw 'Unexpected manifest base version or API level.' }
$info = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stagingPath 'OmniKit.dll'))
if ($info.ProductVersion -ne $Revision -and -not $info.ProductVersion.StartsWith($Revision + '+')) { throw 'Build informational version does not match requested revision.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $stagingPath
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/THIRD-PARTY-NOTICES.md') -Destination $stagingPath
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $stagingPath
Compress-Archive -Path (Join-Path $stagingPath '*') -DestinationPath (Join-Path $distributionPath 'OmniKit.zip') -Force
Copy-Item -LiteralPath $umbraPath -Destination (Join-Path $distributionPath 'OmniKit.Umbra.dll') -Force

$download = "https://github.com/fostot/OmniKit/releases/download/v$Revision/OmniKit.zip"
$feed = [ordered]@{
    Author = 'OmniKit contributors'; Name = 'OmniKit'; InternalName = 'OmniKit'
    AssemblyVersion = '0.1.0.0'; DalamudApiLevel = 15; ApplicableVersion = 'any'
    Punchline = 'Your everyday toolkit for XIV.'
    Description = "Early preview $Revision. Search, optional soft interaction and HUD, gathering gear, moon companions, voice shortcuts and Umbra integration. Game validation is still pending."
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
