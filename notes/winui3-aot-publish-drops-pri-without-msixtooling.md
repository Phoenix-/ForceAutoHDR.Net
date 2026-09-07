# Unpackaged WinUI 3 publish drops the app PRI when EnableMsixTooling=false

**Trap (spike, 2026-09-07, Windows App SDK 2.4.0 / .NET 10 / NativeAOT):** the project
template sets `EnableMsixTooling=true`. It looks MSIX-only, so for an unpackaged app
(`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`) it is tempting to turn it
off. `dotnet build` still produces `bin\...\<App>.pri` (compiled XBF + Assets), but
`dotnet publish` no longer copies it: the PRI pipeline in `Microsoft.WinUI.AppX.targets`
is gated on `EnableMsixTooling=='true'`.

**Symptom:** publish succeeds with zero warnings; the exe exits after ~2 s with
`0xC000027B` (stowed exception), WER shows `combase.dll` / `Microsoft.UI.Xaml.dll`,
HRESULT `0x80004005`. Nothing on stderr because the failure is inside XAML
`InitializeComponent`. Copying `<App>.pri` next to the exe by hand makes it run.

**Fix:** keep `<EnableMsixTooling>true</EnableMsixTooling>` together with
`<WindowsPackageType>None</WindowsPackageType>`. That is the documented combination;
it does not turn the app into an MSIX. (Meridian, on WinAppSDK 1.8, instead added a
custom `_IncludePriAndXbfInPublish` target; on 2.4 that is not needed.)

**Where the guard lives:** for now only in the throwaway WinUI 3 spike that proved this
(kept out of the repo). Carry the property into the real app csproj with the comment
saying *why* it is true — `EnableMsixTooling=true` next to `WindowsPackageType=None`
reads like a mistake to anyone tidying up later, and that is exactly how this trap is
walked into a second time.

**Diagnosing WinUI startup crashes in general:** stderr of a NativeAOT WinExe *does*
carry managed unhandled exceptions when you start it with redirected stderr
(`Start-Process -RedirectStandardError`), and the Event Log `Application Error 1000`
names the faulting module. Use both before guessing.
