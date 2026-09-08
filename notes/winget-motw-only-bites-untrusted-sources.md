# winget's Mark-of-the-Web scan only runs for untrusted sources

**Trap (2026-09-08, winget-cli master):** winget applies a Mark-of-the-Web to every installer
it downloads, and the code path that does it forks on how much the *source* is trusted, not on
the installer type. From `src/AppInstallerCLICore/Workflows/DownloadFlow.cpp`, in
`UpdateInstallerFileMotwIfApplicable`:

```cpp
if (WI_IsFlagSet(context.GetFlags(), Execution::ContextFlag::InstallerTrusted))
{
    // We know the installer already went through multiple scans and we can trust it.
    Utility::ApplyMotwIfApplicable(..., URLZONE_TRUSTED);
}
else if (WI_IsFlagSet(context.GetFlags(), Execution::ContextFlag::InstallerHashMatched))
{
    // IAttachmentExecute performs some additional scans before setting MotW, for example invoking anti-virus.
    HRESULT hr = Utility::ApplyMotwUsingIAttachmentExecuteIfApplicable(..., URLZONE_INTERNET);
```

`InstallerTrusted` is set when the source's `SourceTrustLevel` is `Trusted`. The official
community source (`winget`) reports `Trusted|StoreOrigin`, so a package installed from
winget-pkgs takes the first branch: a plain zone write, no antivirus invocation, no SmartScreen.

**Symptom when you do hit the second branch:** `winget install` either fails with
`InstallerFailedVirusScan` / `InstallerBlockedByPolicy` / `InstallerFailedSecurityCheck`
(`APPINSTALLER_CLI_ERROR_INSTALLER_SECURITY_CHECK_FAILED`), or hangs forever on a GUI
"Windows protected your PC" dialog that never gets dismissed in an unattended session
(microsoft/winget-cli#4046). The log stops at `Started applying motw using IAttachmentExecute to...`.

**Why this matters here:** our installers are unsigned, and unsigned + no SmartScreen reputation
is exactly what trips that branch. The branch is reached by `winget install --manifest` and by
custom or private sources — which is to say, by *our own local testing of the manifest*, and not
by anyone installing the published package. Do not read a local `--manifest` failure as "the
package is broken"; the same bytes from the community repo never run that scan.

winget acknowledges the asymmetry in its own CLI: `winget install --manifest` is gated behind
`winget settings --enable LocalManifestFiles` (elevated, once), and carries a flag that exists for
no other case — `--ignore-local-archive-malware-scan`, described as "ignore the malware scan
performed as part of installing an archive type package from local manifest". There is no such
flag for the community repository, because there is no such scan there.

This is also why unsigned is a viable answer for us at all, and why signing was not made a
prerequisite for shipping a winget package. See microsoft/winget-pkgs#385483 for a project
(Halloy) that read a local failure as a release blocker and paused their winget updates over it.

**Where the guard lives:** `packaging/winget/README.md` repeats the short version next to the
test command, because that is where the wrong conclusion gets drawn.
