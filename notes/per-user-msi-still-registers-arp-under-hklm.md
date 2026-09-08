# A per-user MSI still registers its ARP entry under HKLM

**Trap (2026-09-08, Windows 11 26200, WiX v7.0.0):** the installer is authored
`Package/@Scope="perUser"`, which sets `InstallPrivileges="limited"`. That part works exactly as
intended -- `msiexec /i ... /qn` from an unelevated shell installs with no prompt, and the verbose
log confirms it:

```
MSI (s) (6C:90): Running product '{44359A78-...}' with user privileges: It's not assigned.
```

`AlwaysInstallElevated` is 0 in both the machine and user policy branches, so nothing is silently
elevating. Files land in `%LOCALAPPDATA%\Programs\ForceAutoHDR.Net`.

**But the Add/Remove Programs key does not follow the install into HKCU.** Measured immediately
after a successful unelevated install:

| Key | Present |
|---|---|
| `HKCU\...\CurrentVersion\Uninstall\{ProductCode}` | no |
| `HKLM\...\CurrentVersion\Uninstall\{ProductCode}` | **yes** |
| `HKLM\...\Installer\UserData\<user SID>\Products\...\InstallProperties` | yes |
| `HKLM\...\Installer\UserData\S-1-5-18\Products\...` (SYSTEM, i.e. per-machine) | no |

The `UserData` rows are the ones that describe the actual scope, and they say per-user. The
Uninstall key going to HKLM is Windows Installer's own behaviour; the authoring does not control
it, and switching to `Scope="perUserOrMachine"` does not change it either (tried).

**What it costs:** winget reads the Uninstall key, so it reports the installed package as
`ARP\Machine\X64\{ProductCode}` no matter what. That makes `Scope` in the winget manifest a trap
in both directions -- `user` contradicts what winget observes and can break `winget upgrade`
correlation, `machine` would make winget ask for elevation this installer does not need and must
not have.

**Fix:** leave `Scope` out of the winget installer manifest entirely. It is an optional field.
Correlation then happens through `ProductCode` and `AppsAndFeaturesEntries`, which are exact, and
winget asserts nothing about elevation.

**Where the guard lives:** the comment next to the absent `Scope` in
[packaging/winget/MikhailKazakov.ForceAutoHDRNet.installer.yaml](../packaging/winget/MikhailKazakov.ForceAutoHDRNet.installer.yaml),
and the Scope comment in [packaging/msi/Package.wxs](../packaging/msi/Package.wxs).
