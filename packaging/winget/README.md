# winget manifests

Templates for the [winget community repository](https://github.com/microsoft/winget-pkgs) entry.
`render.ps1` fills in the version, the installer URL and its SHA256, and lays the three files out
in the folder structure winget-pkgs expects. The release workflow runs it for every stable tag and
uploads the result as a build artifact, so submitting a release is a copy and a pull request.

## Why the package is an MSI and not the release zip

A `zip` + `NestedInstallerType: portable` package was built first and rejected, for two reasons
that both come down to portable being a command-line shape:

- **No Start Menu shortcut, and no way to ask for one.** There is no manifest field; the feature
  has been requested repeatedly and is not implemented
  ([winget-cli#2299](https://github.com/microsoft/winget-cli/issues/2299),
  [#4185](https://github.com/microsoft/winget-cli/issues/4185),
  [#4930](https://github.com/microsoft/winget-cli/issues/4930)).
- **Every portable configuration touches PATH.** From `PortableInstaller.cpp`:
  `ArchiveBinariesDependOnPath: true` adds the install directory to PATH; leaving it unset creates
  a symlink in `WinGet\Links`, which is itself on PATH, and falls back to adding the install
  directory to PATH when the symlink cannot be created — which is the common case, since symlinks
  need Developer Mode or admin ([winget-cli#2401](https://github.com/microsoft/winget-cli/pull/2401)).
  There is no PATH-free portable install. For this payload the directory that lands on PATH holds
  thirty Windows App SDK DLLs, which is not somewhere a rarely-used GUI utility belongs.

The symlink default is not an option anyway: an exe started through a `WinGet\Links` symlink does
not find DLLs next to the real file ([winget-cli#2711](https://github.com/microsoft/winget-cli/issues/2711)),
and for this app that is a silent `0xC000027B` at startup.

The MSI gives a Start Menu shortcut, a real Add/Remove Programs entry and no PATH entry at all,
per-user and without an elevation prompt. See [packaging/msi/README.md](../msi/README.md).

## Rendering

The ProductCode and UpgradeCode are generated when the MSI is built, so they are read back out of
it rather than written down anywhere:

```powershell
$msi = '..\msi\bin\Release\ForceAutoHDR.msi'
$codes = ..\msi\Get-MsiProperty.ps1 -Path $msi
./render.ps1 -Version 0.2.0 `
             -InstallerUrl https://github.com/Phoenix-/ForceAutoHDR.Net/releases/download/v0.2.0/ForceAutoHDR-0.2.0-win-x64.msi `
             -InstallerSha256 (Get-FileHash $msi -Algorithm SHA256).Hash `
             -ProductCode $codes.ProductCode `
             -UpgradeCode $codes.UpgradeCode
```

Both codes go into the rendered YAML quoted. A bare `{GUID}` is a YAML flow mapping rather than a
string, and `winget validate` rejects it with "Value type not permitted by 'type' constraint".

Output lands in `out/manifests/m/MikhailKazakov/ForceAutoHDRNet/<version>/`. `out/` is gitignored.

The identifier is `MikhailKazakov.ForceAutoHDRNet`. Two decisions are baked into that.

**No dots inside the package name.** Dots in a `PackageIdentifier` become folder separators in
winget-pkgs, so `MikhailKazakov.ForceAutoHDR.Net` would submit as
`manifests/m/MikhailKazakov/ForceAutoHDR/Net/`, with `Net` as a stray leaf directory. The display
name stays `ForceAutoHDR.Net` in the locale manifest, and `Moniker: forceautohdr` is the short
name users type.

**A real name rather than the GitHub handle.** Package identifiers are a single global namespace,
and "Phoenix" is a common word and an established software vendor besides. A real name is the
better identifier, and it keeps every package from this author under one
`manifests/m/MikhailKazakov/` folder as more of them appear.

This string is also `Package/@Id` in [Package.wxs](../msi/Package.wxs), from which WiX derives the
MSI's UpgradeCode. The two are deliberately the same, and they must stay in step: renaming one
without the other gives you an MSI that installs *beside* the previous version instead of
upgrading it. **Both are effectively frozen once the package is accepted into winget-pkgs** —
after that, changing them means removing one package and submitting another, with no migration
path for anyone already on the old identifier.

## Testing a rendered manifest

`winget validate` needs nothing special:

```powershell
winget validate --manifest out\manifests\m\MikhailKazakov\ForceAutoHDRNet\0.2.0
```

`winget install --manifest` does. It is gated behind a setting that has to be turned on once, from
an **elevated** prompt:

```powershell
winget settings --enable LocalManifestFiles
```

Then, from an ordinary prompt:

```powershell
winget install --manifest out\manifests\m\MikhailKazakov\ForceAutoHDRNet\0.2.0
```

**A security-check failure here does not mean the package is broken.** Our installers are
unsigned, and `--manifest` counts as an untrusted source, which is the only case where winget runs
the downloaded file through `IAttachmentExecute` (antivirus plus SmartScreen) before installing. A
package installed from the community repository skips that scan entirely, because that source is
`Trusted`. If you see `InstallerFailedVirusScan`, `InstallerBlockedByPolicy` or a
"Windows protected your PC" dialog that never returns, read
[notes/winget-motw-only-bites-untrusted-sources.md](../../notes/winget-motw-only-bites-untrusted-sources.md)
before concluding anything about the manifest. (`--ignore-local-archive-malware-scan` exists for
exactly this, but only for archive-type packages, so it does not apply to the MSI.)

Undo a test install with:

```powershell
winget uninstall MikhailKazakov.ForceAutoHDRNet
```

## Submitting

1. Fork [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs).
2. Copy `out/manifests/...` over the fork's `manifests/`, keeping the folder structure.
3. Open a pull request. Automated validation runs first; a moderator review follows.

Code signing is not required — winget verifies `InstallerSha256`, not Authenticode — but the
validation pipeline does run a Defender scan, and an unsigned Native AOT binary is a plausible
false-positive candidate. If that is what blocks a submission,
[SignPath](https://signpath.io/) signs open source projects for free.
