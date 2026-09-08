# <Files> harvesting fails ICE validation in a per-user MSI

**Trap (2026-09-08, WiX v7.0.0):** `<Files Include="...\**" />` is the WiX v4+ replacement for
harvesting with heat, and it is the right tool for this app: the payload is the trimmed Windows
App SDK file set, which changes whenever `TrimWindowsAppSdkPayload` in `ForceAutoHDR.App.csproj`
changes or the SDK is bumped. Point it at a per-user install location and `wix build` fails with
67 errors:

- `ICE38` — component installs to user profile, must use an HKCU registry key as its KeyPath.
- `ICE64` — the directory is in the user profile but is not listed in the RemoveFile table.
- `ICE91` — the file installs to a per-user directory that does not vary based on ALLUSERS.

These are errors, not warnings; the build produces no .msi.

**What does not fix it:** FireGiant's own documentation for `<Files>` says "using `Files` in
per-user packages generates components and files that do not pass ICE validation. The preferred
solution is to use per-user-or-machine or per-machine-or-user packages." Switching
`Package/@Scope` to `perUserOrMachine` produces *the same three ICEs*, because the install
location still does not vary with ALLUSERS. Making it vary means authoring a per-machine mode,
which this app does not want: everything it writes lives under HKCU and the installer should not
be the one part that asks for admin.

**Fix:** keep `Scope="perUser"` and suppress the three, each checked rather than assumed —
ICE38 wants a keypath only a per-machine package needs, ICE64 is covered by explicit
`RemoveFolder` entries, and ICE91 describes a per-machine mode that does not exist here. The
reasoning is written out in full next to `<SuppressIces>` so nobody has to re-derive it.

**Where the guard lives:** [packaging/msi/ForceAutoHDR.wixproj](../packaging/msi/ForceAutoHDR.wixproj)
and the Scope comment in [packaging/msi/Package.wxs](../packaging/msi/Package.wxs).
