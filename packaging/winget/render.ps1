#Requires -Version 7
<#
.SYNOPSIS
    Renders the winget manifest templates in this folder for one release.

.DESCRIPTION
    The templates carry __VERSION__, __INSTALLER_URL__, __INSTALLER_SHA256__, __RELEASE_DATE__,
    __PRODUCT_CODE__ and __UPGRADE_CODE__ placeholders. This fills them in and writes the three
    manifests to -OutputDirectory, named and laid out the way microsoft/winget-pkgs wants them:

        manifests/p/Phoenix/ForceAutoHDRNet/<version>/

    Submitting is then a copy of that folder into a fork of winget-pkgs. See README.md.

.EXAMPLE
    $msi = '..\msi\bin\Release\ForceAutoHDR.msi'
    $codes = ..\msi\Get-MsiProperty.ps1 -Path $msi
    ./render.ps1 -Version 0.2.0 `
                 -InstallerUrl https://github.com/Phoenix-/ForceAutoHDR.Net/releases/download/v0.2.0/ForceAutoHDR-0.2.0-win-x64.msi `
                 -InstallerSha256 (Get-FileHash $msi -Algorithm SHA256).Hash `
                 -ProductCode $codes.ProductCode `
                 -UpgradeCode $codes.UpgradeCode
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.]+)?$')]
    [string] $Version,

    [Parameter(Mandatory)] [ValidatePattern('^https://')]
    [string] $InstallerUrl,

    [Parameter(Mandatory)] [ValidatePattern('^[A-Fa-f0-9]{64}$')]
    [string] $InstallerSha256,

    # Read out of the built .msi with packaging/msi/Get-MsiProperty.ps1. Braces included.
    [Parameter(Mandatory)] [ValidatePattern('^\{[0-9A-Fa-f-]{36}\}$')]
    [string] $ProductCode,

    [Parameter(Mandatory)] [ValidatePattern('^\{[0-9A-Fa-f-]{36}\}$')]
    [string] $UpgradeCode,

    # winget-pkgs wants an ISO date. Defaults to today in UTC, which is when a release workflow
    # would be running anyway.
    [ValidatePattern('^\d{4}-\d{2}-\d{2}$')]
    [string] $ReleaseDate = [datetime]::UtcNow.ToString('yyyy-MM-dd'),

    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'out')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$identifier = 'Phoenix.ForceAutoHDRNet'

# The partition folder is the first letter of the identifier, lowercased, then the identifier with
# its dots turned into separators. Keeping a single dot in the identifier is why this is one
# folder and not a stray "Net" leaf -- see README.md.
$destination = Join-Path $OutputDirectory (
    Join-Path 'manifests' (Join-Path $identifier.Substring(0, 1).ToLowerInvariant() ($identifier -replace '\.', [IO.Path]::DirectorySeparatorChar)))
$destination = Join-Path $destination $Version

$replacements = @{
    '__VERSION__'          = $Version
    '__INSTALLER_URL__'    = $InstallerUrl
    # wingetcreate and every manifest in the repository use uppercase. The schema accepts either,
    # but matching the convention keeps review diffs boring.
    '__INSTALLER_SHA256__' = $InstallerSha256.ToUpperInvariant()
    '__RELEASE_DATE__'     = $ReleaseDate
    '__PRODUCT_CODE__'     = $ProductCode
    '__UPGRADE_CODE__'     = $UpgradeCode
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null

foreach ($template in Get-ChildItem -Path $PSScriptRoot -Filter "$identifier*.yaml") {
    $text = Get-Content -Path $template.FullName -Raw

    foreach ($token in $replacements.Keys) {
        $text = $text.Replace($token, $replacements[$token])
    }

    if ($text -match '__[A-Z_]+__') {
        throw "$($template.Name) still has an unfilled placeholder: $($Matches[0])"
    }

    # UTF-8 without BOM, LF. winget-pkgs validation accepts both line endings, but the repository
    # is LF and a CRLF file shows up as a whole-file diff.
    $text = $text -replace "`r`n", "`n"
    [IO.File]::WriteAllText((Join-Path $destination $template.Name), $text, [Text.UTF8Encoding]::new($false))

    Write-Host "rendered $($template.Name)"
}

Write-Host "manifests written to $destination"
