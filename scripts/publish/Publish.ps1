param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$AppVersionPrefix = "",
    [string]$AppVersionSuffix = "",
    [string]$AssemblyVersion = "",
    [string]$FileVersion = "",
    [string]$InformationalVersion = "",
    [string]$ArtifactVersionLabel = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\.." )).Path
$projectPath = Join-Path $repoRoot "src\inputor.WinUI\inputor.WinUI.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts\publish"
$portableRoot = Join-Path $artifactsRoot "portable"
$stagingRoot = Join-Path $artifactsRoot "staging"
$installerScriptPath = Join-Path $repoRoot "scripts\publish\New-InnoInstaller.ps1"

function Reset-Directory {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }

    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Exit-OnFailure {
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Get-ProjectPropertyValue {
    param(
        [xml]$ProjectXml,
        [string]$PropertyName
    )

    foreach ($propertyGroup in @($ProjectXml.Project.PropertyGroup)) {
        $property = $propertyGroup.PSObject.Properties[$PropertyName]
        if ($null -ne $property) {
            $value = [string]$property.Value
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                return $value.Trim()
            }
        }
    }

    return ""
}

function Get-ArtifactVersionLabel {
    param(
        [string]$ProjectPath,
        [string]$ConfiguredPrefix,
        [string]$ConfiguredSuffix,
        [string]$ConfiguredLabel,
        [bool]$HasConfiguredSuffix
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredLabel)) {
        return $ConfiguredLabel.Trim()
    }

    [xml]$projectXml = Get-Content -Path $ProjectPath
    $projectPrefix = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName "AppVersionPrefix"
    $projectSuffix = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName "AppVersionSuffix"

    $effectivePrefix = if ([string]::IsNullOrWhiteSpace($ConfiguredPrefix)) { $projectPrefix } else { $ConfiguredPrefix.Trim() }
    $effectiveSuffix = if ($HasConfiguredSuffix) { $ConfiguredSuffix.Trim() } else { $projectSuffix }

    if ([string]::IsNullOrWhiteSpace($effectivePrefix)) {
        throw "Could not resolve AppVersionPrefix from parameters or project file."
    }

    $versionLabel = if ([string]::IsNullOrWhiteSpace($effectiveSuffix)) {
        $effectivePrefix
    }
    else {
        "$effectivePrefix-$effectiveSuffix"
    }

    return ($versionLabel -replace '[^0-9A-Za-z._-]', '-')
}

function New-ZipArchive {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [int]$MaxAttempts = 5
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $tempDestinationPath = "$DestinationPath.$attempt.tmp.zip"
        try {
            if (Test-Path $tempDestinationPath) {
                Remove-Item -Path $tempDestinationPath -Force
            }

            Compress-Archive -Path $SourcePath -DestinationPath $tempDestinationPath -CompressionLevel Optimal

            if (Test-Path $DestinationPath) {
                Remove-Item -Path $DestinationPath -Force
            }

            Move-Item -Path $tempDestinationPath -Destination $DestinationPath
            return
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                throw
            }

            Start-Sleep -Seconds 2
        }
    }
}

function Copy-RequiredPublishResources {
    param(
        [string]$AssemblyName,
        [string]$BuildOutputDirectory,
        [string]$PublishDirectory
    )

    $appPriFileName = "$AssemblyName.pri"
    $appPriPath = Join-Path $BuildOutputDirectory $appPriFileName
    if (!(Test-Path $appPriPath)) {
        throw "Expected WinUI PRI file was not found at $appPriPath"
    }

    Copy-Item -Path $appPriPath -Destination (Join-Path $PublishDirectory $appPriFileName) -Force
}

function Remove-UnusedLocaleDirectories {
    param([string]$PublishDirectory)

    $keptCultures = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($cultureName in @('en', 'en-US', 'zh', 'zh-CN', 'zh-Hans')) {
        $null = $keptCultures.Add($cultureName)
    }

    foreach ($directory in Get-ChildItem -Path $PublishDirectory -Directory) {
        try {
            $culture = [System.Globalization.CultureInfo]::GetCultureInfo($directory.Name)
            if (-not $keptCultures.Contains($culture.Name)) {
                Remove-Item -Path $directory.FullName -Recurse -Force
            }
        }
        catch [System.Globalization.CultureNotFoundException] {
            continue
        }
    }
}

[xml]$projectXml = Get-Content -Path $projectPath
$resolvedArtifactVersionLabel = Get-ArtifactVersionLabel -ProjectPath $projectPath -ConfiguredPrefix $AppVersionPrefix -ConfiguredSuffix $AppVersionSuffix -ConfiguredLabel $ArtifactVersionLabel -HasConfiguredSuffix $PSBoundParameters.ContainsKey('AppVersionSuffix')
$targetFramework = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName 'TargetFramework'
$assemblyName = Get-ProjectPropertyValue -ProjectXml $projectXml -PropertyName 'AssemblyName'
$projectDirectory = Split-Path -Parent $projectPath
$buildOutputDirectory = Join-Path $projectDirectory (Join-Path 'bin' (Join-Path $Configuration (Join-Path $targetFramework $Runtime)))
$portableDirName = "inputor-$resolvedArtifactVersionLabel-portable-$Runtime"
$portablePublishDir = Join-Path $portableRoot $portableDirName
$portableZipPath = Join-Path $artifactsRoot "$portableDirName.zip"
$installerPath = Join-Path $artifactsRoot "inputor-$resolvedArtifactVersionLabel-setup-$Runtime.exe"

if ([string]::IsNullOrWhiteSpace($assemblyName)) {
    throw 'Could not resolve AssemblyName from project file.'
}

Reset-Directory -Path $portableRoot

if (Test-Path $stagingRoot) {
    Remove-Item -Path $stagingRoot -Recurse -Force
}

if (Test-Path $portableZipPath) {
    Remove-Item -Path $portableZipPath -Force
}

if (Test-Path $installerPath) {
    Remove-Item -Path $installerPath -Force
}

Write-Host "Publishing Release app to $portablePublishDir"
$publishArguments = @(
    "publish"
    $projectPath
    "-c"
    $Configuration
    "-r"
    $Runtime
    "--self-contained"
    "true"
    "-p:DebugType=None"
    "-p:DebugSymbols=false"
    "-o"
    $portablePublishDir
)

if (![string]::IsNullOrWhiteSpace($AppVersionPrefix)) {
    $publishArguments += "-p:AppVersionPrefix=$AppVersionPrefix"
}

if ($PSBoundParameters.ContainsKey('AppVersionSuffix')) {
    $publishArguments += "-p:AppVersionSuffix=$AppVersionSuffix"
}

if (![string]::IsNullOrWhiteSpace($AssemblyVersion)) {
    $publishArguments += "-p:AssemblyVersion=$AssemblyVersion"
}

if (![string]::IsNullOrWhiteSpace($FileVersion)) {
    $publishArguments += "-p:FileVersion=$FileVersion"
}

if (![string]::IsNullOrWhiteSpace($InformationalVersion)) {
    $publishArguments += "-p:InformationalVersion=$InformationalVersion"
}

dotnet @publishArguments
Exit-OnFailure

Copy-RequiredPublishResources -AssemblyName $assemblyName -BuildOutputDirectory $buildOutputDirectory -PublishDirectory $portablePublishDir

Remove-UnusedLocaleDirectories -PublishDirectory $portablePublishDir

$publishedExe = Join-Path $portablePublishDir "$assemblyName.exe"
if (!(Test-Path $publishedExe)) {
    throw "Expected published executable not found at $publishedExe"
}

Write-Host "Creating portable zip at $portableZipPath"
New-ZipArchive -SourcePath $portablePublishDir -DestinationPath $portableZipPath

Write-Host "Creating installer at $installerPath"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installerScriptPath -PublishedDir $portablePublishDir -OutputPath $installerPath
Exit-OnFailure

if (!(Test-Path $portableZipPath)) {
    throw "Portable zip was not created: $portableZipPath"
}

if (!(Test-Path $installerPath)) {
    throw "Installer executable was not created: $installerPath"
}

Write-Host "Publish completed."
Write-Host "Portable directory: $portablePublishDir"
Write-Host "Portable zip: $portableZipPath"
Write-Host "Installer exe: $installerPath"
