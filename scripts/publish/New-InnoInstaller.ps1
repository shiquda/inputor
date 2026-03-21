param(
    [Parameter(Mandatory = $true)]
    [string]$PublishedDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\.." )).Path
$innoScriptPath = Join-Path $repoRoot "packaging\installer\inputor.iss"
$publishedExe = Join-Path $PublishedDir "inputor.App.exe"
$outputDirectory = Split-Path -Parent $OutputPath
$outputBaseName = [IO.Path]::GetFileNameWithoutExtension($OutputPath)
$resolvedOutputDirectory = [IO.Path]::GetFullPath($outputDirectory)
$resolvedPublishedDir = [IO.Path]::GetFullPath($PublishedDir)

function Get-IsccPath {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $fallbackPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
        "C:\Program Files\Inno Setup 6\ISCC.exe"
        "D:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    )

    foreach ($fallbackPath in $fallbackPaths) {
        if (Test-Path $fallbackPath) {
            return $fallbackPath
        }
    }

    throw "Inno Setup compiler not found. Install Inno Setup 6 and ensure ISCC.exe is on PATH or available at one of the configured fallback paths."
}

function Exit-OnFailure {
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if (!(Test-Path $innoScriptPath)) {
    throw "Inno Setup script not found: $innoScriptPath"
}

if (!(Test-Path $PublishedDir)) {
    throw "PublishedDir does not exist: $PublishedDir"
}

if (!(Test-Path $publishedExe)) {
    throw "Published executable not found: $publishedExe"
}

New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

if (Test-Path $OutputPath) {
    Remove-Item -Path $OutputPath -Force
}

$version = (Get-Item $publishedExe).VersionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.0.0"
}

$isccPath = Get-IsccPath

$isccArguments = @(
    "/DPublishedDir=$resolvedPublishedDir"
    "/DAppVersion=$version"
    "/DOutputDir=$resolvedOutputDirectory"
    "/DOutputBaseFilename=$outputBaseName"
    $innoScriptPath
)

& $isccPath @isccArguments
Exit-OnFailure

if (!(Test-Path $OutputPath)) {
    throw "Inno Setup did not create the installer: $OutputPath"
}
