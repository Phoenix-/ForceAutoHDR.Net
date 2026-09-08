#Requires -Version 7
<#
.SYNOPSIS
    Reads properties out of a built MSI.

.DESCRIPTION
    The winget manifest wants the ProductCode and UpgradeCode so that winget can correlate the
    package with its Add/Remove Programs entry for `winget list` and `winget upgrade`. Both are
    generated at build time -- the ProductCode changes with every version by design, and the
    UpgradeCode is derived from Package/@Id -- so they have to be read back out of the .msi
    rather than written down anywhere.

    Uses the WindowsInstaller COM automation, which is present on any Windows machine and needs
    no WiX tooling.

.EXAMPLE
    ./Get-MsiProperty.ps1 -Path bin/Release/ForceAutoHDR.msi
    ./Get-MsiProperty.ps1 -Path bin/Release/ForceAutoHDR.msi -Name ProductCode
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Path,

    # Omit to get every property this script knows about, as an ordered hashtable.
    [ValidateSet('ProductCode', 'UpgradeCode', 'ProductVersion', 'ProductName', 'Manufacturer')]
    [string] $Name
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$full = (Resolve-Path -LiteralPath $Path).ProviderPath

$installer = New-Object -ComObject WindowsInstaller.Installer
$invoke = {
    param($target, $member, $arguments)
    $target.GetType().InvokeMember($member, 'InvokeMethod', $null, $target, $arguments)
}

# 0 = read-only.
$database = & $invoke $installer 'OpenDatabase' @($full, 0)

function Read-Property([string] $property) {
    $view = & $invoke $database 'OpenView' @("SELECT Value FROM Property WHERE Property = '$property'")
    & $invoke $view 'Execute' @() | Out-Null
    $record = & $invoke $view 'Fetch' @()
    if ($null -eq $record) { return $null }
    $record.GetType().InvokeMember('StringData', 'GetProperty', $null, $record, @(1))
}

$names = if ($Name) { @($Name) } else { 'ProductCode', 'UpgradeCode', 'ProductVersion', 'ProductName', 'Manufacturer' }

if ($Name) {
    Read-Property $Name
}
else {
    $result = [ordered] @{}
    foreach ($n in $names) { $result[$n] = Read-Property $n }
    [pscustomobject] $result
}
