# Changing only ProductVersion does not rebuild the MSI

**Trap (2026-09-08, WiX v7.0.0 / MSBuild):** the MSI takes its version from an MSBuild property:

```
dotnet build packaging/msi/ForceAutoHDR.wixproj -c Release -p:ProductVersion=0.2.0 -p:PayloadDir=...
```

Run that after a build at 0.1.0 and MSBuild reports success in a couple of seconds, having done
nothing. The property reaches the WiX preprocessor through `DefineConstants`, but MSBuild's
up-to-date check only looks at file timestamps, and no file changed. The .msi on disk is the old
one, still stamped 0.1.0.

**Symptom:** none at build time. Zero errors, zero warnings, an .msi in `bin\Release`. The
version is only wrong once someone installs it, and then it is wrong in the worst way: Windows
Installer sees the previous version already present and either refuses the "downgrade" or
no-ops the upgrade.

**Fix:** always build the MSI with `-t:Rebuild`. The release workflow does, and it also reads
`ProductVersion` back out of the finished .msi with `packaging/msi/Get-MsiProperty.ps1` and throws
if it does not match the tag, because a guard that is only a comment gets edited away.

**Where the guard lives:** the `Build the MSI` step in
[.github/workflows/release.yml](../.github/workflows/release.yml). Verified by building 0.1.0,
then 0.2.0 without `-t:Rebuild` (still reported 0.1.0), then with it (0.2.0, and a fresh
ProductCode).
