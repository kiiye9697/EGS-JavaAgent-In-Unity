param(
    [Parameter(Mandatory = $true)]
    [string]$UnityProjectRoot,
    [string]$PortableJdkRoot = "C:\tmp\jdk21-portable\jdk-21.0.11+10"
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    return [System.IO.Path]::GetFullPath($PathValue)
}

$repoRoot = Resolve-AbsolutePath -PathValue (Join-Path $PSScriptRoot "..")
$sourceRoot = Join-Path $repoRoot "unity-client\Assets\EGS\JavaAgent"
$javaAgentDistRoot = Join-Path $repoRoot "java-agent\build\install\egs-java-agent"
$targetProjectRoot = Resolve-AbsolutePath -PathValue $UnityProjectRoot
$targetAssetsRoot = Join-Path $targetProjectRoot "Assets"
$targetRoot = Join-Path $targetAssetsRoot "EGS\JavaAgent"
$targetEmbeddedRoot = Join-Path $targetRoot "Embedded"
$projectSettingsPath = Join-Path $targetProjectRoot "ProjectSettings\ProjectSettings.asset"

if (!(Test-Path -LiteralPath $sourceRoot)) {
    throw "Unity client source folder was not found: $sourceRoot"
}

if (!(Test-Path -LiteralPath $targetProjectRoot)) {
    throw "Unity project root does not exist: $targetProjectRoot"
}

if (!(Test-Path -LiteralPath $targetAssetsRoot)) {
    throw "Target project is missing an Assets folder: $targetAssetsRoot"
}

if (!(Test-Path -LiteralPath $projectSettingsPath)) {
    throw "Target folder does not look like a Unity project because ProjectSettings/ProjectSettings.asset was not found."
}

if (!(Test-Path -LiteralPath $javaAgentDistRoot)) {
    throw "Java agent installDist output was not found: $javaAgentDistRoot"
}

if (!(Test-Path -LiteralPath $PortableJdkRoot)) {
    throw "Portable JDK root was not found: $PortableJdkRoot"
}

if (Test-Path -LiteralPath $targetRoot) {
    Remove-Item -Recurse -Force -LiteralPath $targetRoot
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetRoot) | Out-Null
Copy-Item -Recurse -Force -LiteralPath $sourceRoot -Destination $targetRoot
New-Item -ItemType Directory -Force -Path $targetEmbeddedRoot | Out-Null
Copy-Item -Recurse -Force -LiteralPath $javaAgentDistRoot -Destination (Join-Path $targetEmbeddedRoot "egs-java-agent")
Copy-Item -Recurse -Force -LiteralPath $PortableJdkRoot -Destination (Join-Path $targetEmbeddedRoot "jdk")

Write-Output "Unity client deployed successfully."
Write-Output "Source: $sourceRoot"
Write-Output "Target: $targetRoot"
Write-Output "Bundled Java Agent Dist: $(Join-Path $targetEmbeddedRoot 'egs-java-agent')"
Write-Output "Bundled JDK: $(Join-Path $targetEmbeddedRoot 'jdk')"
