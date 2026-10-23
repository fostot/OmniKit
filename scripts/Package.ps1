[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Get-GitValue([string[]]$Arguments) {
    $value = & git -C $projectRoot @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Could not read source Git metadata.' }
    return ($value -join "`n").TrimEnd()
}
if ((Get-GitValue @('rev-parse', '--is-shallow-repository')) -ne 'false') { throw 'Packaging requires complete Git history.' }
$sourceCommit = Get-GitValue @('rev-parse', 'HEAD')
$shortCommit = Get-GitValue @('rev-parse', '--short=7', 'HEAD')
$commitCount = [int](Get-GitValue @('rev-list', '--count', 'HEAD'))
if ($commitCount -gt 65534) { throw 'Commit count exceeds the numeric assembly revision capacity; do not truncate or wrap it.' }
[xml]$versionProject = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw
$expectedBaseVersion = [string]$versionProject.Project.PropertyGroup.OmniKitBaseVersion
$Revision = $expectedBaseVersion + '.' + $commitCount.ToString('D4')
$expectedAssemblyVersion = $expectedBaseVersion + '.' + $commitCount
$displayVersion = "$Revision ($shortCommit)"
$uncommitted = @(Get-GitValue @('status', '--porcelain', '--untracked-files=normal')) -split "`n" |
    Where-Object { $_ -and $_ -notmatch '^.. repo\.json$' }
if ($uncommitted) { throw 'Commit and review source changes before packaging. Only the installer feed may be uncommitted.' }

$releasePath = Join-Path $projectRoot 'src/OmniKit/bin/Release'
$umbraPath = Join-Path $projectRoot 'src/OmniKit.Umbra/bin/Release/OmniKit.Umbra.dll'
$buildMetadataPath = Join-Path $releasePath 'OmniKit.build.json'
$build = Get-Content -LiteralPath $buildMetadataPath -Raw | ConvertFrom-Json
if ($build.CommitSha -cne $sourceCommit -or $build.CommitCount -ne $commitCount -or
    $build.ArtifactVersion -cne $Revision -or $build.AssemblyVersion -cne $expectedAssemblyVersion -or
    $build.DisplayVersion -cne $displayVersion -or $build.Author -cne 'Zero') { throw 'Build metadata does not match the current source commit. Rebuild both plugins.' }
$umbraBuild = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $umbraPath) 'OmniKit.Umbra.build.json') -Raw | ConvertFrom-Json
if ($umbraBuild.CommitSha -cne $sourceCommit -or $umbraBuild.CommitCount -ne $commitCount -or
    $umbraBuild.ArtifactVersion -cne $Revision -or $umbraBuild.AssemblyVersion -cne $expectedAssemblyVersion -or
    $umbraBuild.DisplayVersion -cne $displayVersion -or $umbraBuild.Author -cne 'Zero') { throw 'Umbra companion was built from a different source commit.' }
$coreFileName = "OmniKit-$Revision-$shortCommit.zip"
$umbraFileName = "OmniKit.Umbra-$Revision-$shortCommit.dll"
$stagingPath = Join-Path $projectRoot ('.local/package-' + [guid]::NewGuid().ToString('N'))
$distributionPath = Join-Path $projectRoot ('dist/' + $Revision)
if (Test-Path -LiteralPath $distributionPath) { throw 'This distribution revision already exists. Preserve it and package a new internal revision.' }

# An explicit runtime allowlist avoids shipping reference libraries, settings,
# PDBs, developer output, unrelated runtimes or a prior package recursively.
$runtimeFiles = @(
    'OmniKit.dll', 'OmniKit.json', 'OmniKit.deps.json', 'OmniKit.build.json',
    'Microsoft.Data.Sqlite.dll', 'SQLitePCLRaw.batteries_v2.dll',
    'SQLitePCLRaw.core.dll', 'SQLitePCLRaw.provider.e_sqlite3.dll', 'e_sqlite3.dll',
    'System.Security.Cryptography.ProtectedData.dll', 'System.Speech.dll',
    'assets/bimble.png', 'assets/moonbear.png', 'assets/companions-walk.png',
    'assets/bimble-motion.png', 'assets/moonbear-motion.png', 'assets/bimble-stargazer.png', 'assets/moonbear-stargazer.png',
    'assets/bimble-physical.png', 'assets/moonbear-physical.png',
    'assets/bimble-physical-stargazer.png', 'assets/moonbear-physical-stargazer.png',
    'assets/moonblob-motion.png', 'assets/wink-motion.png', 'assets/nova-motion.png',
    'assets/abstract-companions.frames.json', 'assets/lunar-companions.frames.json',
    'assets/bimble-walk-rig.png', 'assets/bimble-walk-rig-stargazer.png',
    'assets/moonbear-walk-rig.png', 'assets/moonbear-walk-rig-stargazer.png',
    'assets/moonblob-limbless.png', 'assets/lunar-walk-rigs.json'
)
foreach ($relative in $runtimeFiles) {
    $source = Join-Path $releasePath $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing build file: $relative" }
}
if (-not (Test-Path -LiteralPath $umbraPath -PathType Leaf)) { throw 'Missing Umbra companion build.' }
$manifest = Get-Content -LiteralPath (Join-Path $releasePath 'OmniKit.json') -Raw | ConvertFrom-Json
$manifestAssemblyVersion = [string]$manifest.AssemblyVersion
if ($manifestAssemblyVersion -cne $expectedAssemblyVersion -or $manifest.DalamudApiLevel -ne 15 -or $manifest.Author -cne 'Zero') { throw 'Manifest version, author or API level does not match the expected build.' }
foreach ($binary in @((Join-Path $releasePath 'OmniKit.dll'), $umbraPath)) {
    $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($binary).Version.ToString()
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($binary)
    if ($assemblyVersion -cne $expectedAssemblyVersion -or $info.FileVersion -cne $expectedAssemblyVersion) { throw 'DLL assembly/file versions do not match the expected build.' }
    if ($info.ProductVersion -cne $displayVersion -or $info.CompanyName -cne 'Zero') { throw 'DLL source version or author does not match the expected build.' }
}

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
Compress-Archive -Path (Join-Path $stagingPath '*') -DestinationPath (Join-Path $distributionPath $coreFileName)
Copy-Item -LiteralPath $umbraPath -Destination (Join-Path $distributionPath $umbraFileName)

$download = "https://github.com/fostot/OmniKit/releases/download/v$Revision/$coreFileName"
$feed = [ordered]@{
    Author = 'Zero'; Name = 'OmniKit'; InternalName = 'OmniKit'
    AssemblyVersion = $manifestAssemblyVersion; DalamudApiLevel = 15; ApplicableVersion = 'any'
    Punchline = 'Your everyday toolkit for XIV.'
    Description = "Preview $displayVersion by Zero. Minimal search, configurable target types, styled target HUD, animated frame companions, optional AI connections, gathering gear, voice shortcuts and Umbra integration."
    RepoUrl = 'https://github.com/fostot/OmniKit'
    Tags = @('search', 'utility', 'accessibility', 'umbra')
    DownloadLinkInstall = $download; DownloadLinkUpdate = $download; DownloadLinkTesting = $download
    IconUrl = 'https://raw.githubusercontent.com/fostot/OmniKit/main/assets/bimble.png'
    LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    IsHide = $false; IsTestingExclusive = $false
}
ConvertTo-Json -InputObject @($feed) -Depth 5 | Set-Content -LiteralPath (Join-Path $projectRoot 'repo.json') -Encoding utf8NoBOM
$hashes = @($coreFileName, $umbraFileName) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath (Join-Path $distributionPath $_) -Algorithm SHA256
    $hash.Hash.ToLowerInvariant() + '  ' + $_
}
$hashes | Set-Content -LiteralPath (Join-Path $distributionPath 'SHA256SUMS.txt') -Encoding utf8NoBOM
Write-Output "Packaged $displayVersion from source commit $sourceCommit. Review dist/$Revision and repo.json before publishing."
