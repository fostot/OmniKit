[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $PSScriptRoot
$fixtureRoot = Join-Path $projectRoot ('.local/versioning-tests-' + [guid]::NewGuid().ToString('N'))
$createdPaths = [Collections.Generic.List[string]]::new()

function New-FixtureDirectory([string]$Path) {
    $absolute = [IO.Path]::GetFullPath($Path)
    if (-not $absolute.StartsWith([IO.Path]::GetFullPath($fixtureRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        $absolute -cne [IO.Path]::GetFullPath($fixtureRoot)) { throw 'Fixture path is outside this test run.' }
    New-Item -ItemType Directory -Path $absolute | Out-Null
    $createdPaths.Add($absolute)
    return $absolute
}

function Invoke-FixtureTool([string]$Name, [string]$Directory, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Name
    $start.WorkingDirectory = $Directory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult() }
    }
    finally { $process.Dispose() }
}

function Invoke-FixtureGit([string]$Directory, [string[]]$Arguments) {
    $result = Invoke-FixtureTool 'git' $Directory $Arguments
    if ($result.ExitCode -ne 0) { throw ('Fixture Git command failed: ' + $result.Output) }
    return $result.Output.Trim()
}

function Write-FixtureProject([string]$Directory) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'Directory.Build.props'), (Join-Path $projectRoot 'Directory.Build.targets') -Destination $Directory
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $Directory 'OmniKit.csproj') -Encoding utf8NoBOM
    'public static class Marker { public const string Value = "Version fixture"; }' |
        Set-Content -LiteralPath (Join-Path $Directory 'Marker.cs') -Encoding utf8NoBOM
    "bin/`nobj/" | Set-Content -LiteralPath (Join-Path $Directory '.gitignore') -Encoding utf8NoBOM
}

function New-FixtureCommit([string]$Directory, [string]$Message) {
    Invoke-FixtureGit $Directory @('-c', 'user.name=VersionTest', '-c', 'user.email=version-test@example.invalid',
        '-c', 'commit.gpgsign=false', '-c', "core.hooksPath=$emptyGitDirectory", 'commit', '--quiet', '-m', $Message) | Out-Null
}

function Assert-FixtureBuild([string]$Directory, [int]$Count, [string]$ArtifactVersion) {
    $expectedSha = Invoke-FixtureGit $Directory @('rev-parse', 'HEAD')
    $expectedShort = Invoke-FixtureGit $Directory @('rev-parse', '--short=7', 'HEAD')
    $build = Invoke-FixtureTool 'dotnet' $Directory @('build', 'OmniKit.csproj', '-c', 'Release', '--nologo', '--verbosity', 'quiet')
    if ($build.ExitCode -ne 0) { throw ('Fixture build failed: ' + $build.Output) }
    $outputDirectory = Join-Path $Directory 'bin/Release/net10.0'
    $metadata = Get-Content -LiteralPath (Join-Path $outputDirectory 'OmniKit.build.json') -Raw | ConvertFrom-Json
    $numeric = '0.1.0.' + $Count
    $display = "$ArtifactVersion ($expectedShort)"
    if ($metadata.BaseVersion -cne '0.1.0' -or $metadata.CommitCount -ne $Count -or
        $metadata.CommitSha -cne $expectedSha -or $metadata.ShortSha -cne $expectedShort -or
        $metadata.ArtifactVersion -cne $ArtifactVersion -or $metadata.AssemblyVersion -cne $numeric -or
        $metadata.DisplayVersion -cne $display -or $metadata.Author -cne 'Zero') { throw 'Generated build metadata does not identify the fixture source commit.' }
    $dll = Join-Path $outputDirectory 'OmniKit.dll'
    $file = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll)
    if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString() -cne $numeric -or
        $file.FileVersion -cne $numeric -or $file.ProductVersion -cne $display) { throw 'Built DLL versions disagree with its source metadata.' }
    # Loading bytes avoids locking the fixture DLL before the incremental second build.
    $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($dll))
    $attributes = @($assembly.GetCustomAttributesData() | Where-Object { $_.AttributeType.FullName -eq 'System.Reflection.AssemblyMetadataAttribute' })
    $commitAttributes = @($attributes | Where-Object { $_.ConstructorArguments[0].Value -eq 'OmniKit.CommitSha' })
    $countAttributes = @($attributes | Where-Object { $_.ConstructorArguments[0].Value -eq 'OmniKit.CommitCount' })
    if ($commitAttributes.Count -ne 1 -or $countAttributes.Count -ne 1 -or
        $commitAttributes[0].ConstructorArguments[1].Value -cne $expectedSha -or
        $countAttributes[0].ConstructorArguments[1].Value -cne [string]$Count) { throw 'Embedded assembly source attributes do not match Git.' }
    Write-Output "PASS: actual MSBuild targets, emitted metadata and DLL versions at $Count fixture commit(s)."
}

New-FixtureDirectory $fixtureRoot | Out-Null
$emptyGitDirectory = New-FixtureDirectory (Join-Path $fixtureRoot 'empty-git-template')
try {
    $source = New-FixtureDirectory (Join-Path $fixtureRoot 'source')
    Write-FixtureProject $source
    Invoke-FixtureGit $source @('init', '--quiet', '--initial-branch=main', "--template=$emptyGitDirectory") | Out-Null
    Invoke-FixtureGit $source @('add', '--', 'Directory.Build.props', 'Directory.Build.targets', 'OmniKit.csproj', 'Marker.cs', '.gitignore') | Out-Null
    New-FixtureCommit $source 'Synthetic version fixture source'
    Assert-FixtureBuild $source 1 '0.1.0.0001'

    # A non-code commit must still invalidate incremental assembly version metadata.
    'Synthetic metadata-only change.' | Set-Content -LiteralPath (Join-Path $source 'fixture-note.txt') -Encoding utf8NoBOM
    Invoke-FixtureGit $source @('add', '--', 'fixture-note.txt') | Out-Null
    New-FixtureCommit $source 'Synthetic metadata-only fixture update'
    Assert-FixtureBuild $source 2 '0.1.0.0002'

    $withoutGit = New-FixtureDirectory (Join-Path $fixtureRoot 'without-git')
    Write-FixtureProject $withoutGit
    $missingResult = Invoke-FixtureTool 'dotnet' $withoutGit @('build', 'OmniKit.csproj', '-c', 'Release', '--nologo', '--verbosity', 'quiet')
    if ($missingResult.ExitCode -eq 0 -or $missingResult.Output -notmatch 'requires a Git checkout with full history') {
        throw ('Missing-Git fixture was not rejected for the expected reason: ' + $missingResult.Output)
    }
    Write-Output 'PASS: a source directory without its own Git metadata is rejected.'

    $shallow = Join-Path $fixtureRoot 'shallow'
    $sourceUri = ([Uri]([IO.Path]::GetFullPath($source) + [IO.Path]::DirectorySeparatorChar)).AbsoluteUri
    Invoke-FixtureGit $fixtureRoot @('-c', 'protocol.file.allow=always', 'clone', '--quiet', '--depth', '1', '--no-local', "--template=$emptyGitDirectory", $sourceUri, $shallow) | Out-Null
    $createdPaths.Add([IO.Path]::GetFullPath($shallow))
    if ((Invoke-FixtureGit $shallow @('rev-parse', '--is-shallow-repository')) -cne 'true') { throw 'The shallow fixture does not have shallow history.' }
    $shallowResult = Invoke-FixtureTool 'dotnet' $shallow @('build', 'OmniKit.csproj', '-c', 'Release', '--nologo', '--verbosity', 'quiet')
    if ($shallowResult.ExitCode -eq 0 -or $shallowResult.Output -notmatch 'requires complete Git history') {
        throw ('Shallow checkout was not rejected for the expected reason: ' + $shallowResult.Output)
    }
    Write-Output 'PASS: the actual targets reject a shallow file:// clone.'
}
finally {
    # Fixtures contain only synthetic source/identities and remain ignored for inspection.
    $createdPaths | Set-Content -LiteralPath (Join-Path $fixtureRoot 'created-paths.txt') -Encoding utf8NoBOM
    Write-Output ("Synthetic fixtures retained under .local/" + (Split-Path -Leaf $fixtureRoot) + '.')
}
