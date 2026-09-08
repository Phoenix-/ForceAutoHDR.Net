# MSI installer

A per-user MSI built with WiX. It is what the winget package installs, and it is the reason the
app gets a Start Menu shortcut and an Add/Remove Programs entry that a portable zip cannot give it.

## Shape

- **Per-user, never elevated.** `Scope="perUser"` sets `InstallPrivileges="limited"`. Installs
  into `%LOCALAPPDATA%\Programs\ForceAutoHDR.Net`. Everything the app writes at runtime lives
  under HKCU, so an installer asking for admin would be the only part of the product that did.
  Verified from an unelevated shell: no prompt, exit 0, and the log says *"Running product
  '{...}' with user privileges: It's not assigned."* One wrinkle worth knowing about: Windows
  Installer writes the Add/Remove Programs key to HKLM anyway, so winget sees the package as
  `ARP\Machine`. That is why the winget manifest declares no `Scope` —
  [notes/per-user-msi-still-registers-arp-under-hklm.md](../../notes/per-user-msi-still-registers-arp-under-hklm.md).
- **No PATH entry.** This was the deciding difference against a winget `portable` package: every
  portable configuration puts something on PATH, and for this payload that something is a
  directory holding thirty Windows App SDK DLLs.
- **Payload harvested, not listed.** `<Files Include="!(bindpath.payload)\**" />` picks up
  whatever `dotnet publish` produced, so the trim list in `ForceAutoHDR.App.csproj` stays the
  single source of truth for what ships.
- **Upgrades handled by Windows Installer.** `Package/@Id` is a stable string, WiX derives the
  UpgradeCode from it, and `<MajorUpgrade>` replaces the previous version. Verified: building
  0.1.0 and 0.2.0 gives the same UpgradeCode and different ProductCodes.

## Building

The payload has to exist first; the MSI is built from a publish output, not from a project
reference, because the file set only settles after the AOT link step and the trim target:

```powershell
dotnet publish src/ForceAutoHDR.App -c Release -o publish
dotnet build packaging/msi/ForceAutoHDR.wixproj -c Release -t:Rebuild -p:ProductVersion=0.2.0 -p:PayloadDir=$PWD\publish
```

`-t:Rebuild` is not optional. See
[notes/msi-rebuild-needed-when-only-the-version-changes.md](../../notes/msi-rebuild-needed-when-only-the-version-changes.md).

To read the generated codes back out, for a winget manifest or just to check what got built:

```powershell
./Get-MsiProperty.ps1 -Path bin/Release/ForceAutoHDR.msi
```

## The WiX licence

WiX v7 participates in the [Open Source Maintenance Fee](https://opensourcemaintenancefee.org/).
The fee itself is owed by organizations above a revenue threshold (typically US$10,000) that use
the binary releases as part of revenue generating activities. A free hobby project run by one
person is not that, so nothing is owed here.

What *is* required of everyone is accepting the EULA: from v7 on, `wix` refuses to run any command
until it is accepted, with `error WIX7015`. That is the single `<AcceptEula>wix7</AcceptEula>` line
in the wixproj. If that obligation ever becomes unwelcome, WiX v5 is the last release before the
scheme was introduced.

## Why not Inno Setup or a plain zip

Inno produces an unsigned `.exe`, which is the one installer shape that reliably trips SmartScreen
for users who download it by hand. The zip is still built and still published — it is what the
README tells people to grab, and it stays the answer for anyone who does not want an installer at
all. The MSI exists for the people who want the app to behave like an installed application.
