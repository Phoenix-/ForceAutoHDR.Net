# Trimming the self-contained Windows App SDK payload (NativeAOT, unpackaged)

**Measured (spike, 2026-09-07, hello-world with TitleBar + Mica + Frame/Page):**

| Variant | Files | Raw | 7z | zip |
|---|---|---|---|---|
| Meta-package `Microsoft.WindowsAppSDK` 2.4.0, untouched publish | 289 | 131.7 MB (19 MB is pdb) | 28 MB | 43 MB |
| Component packages + `TrimWindowsAppSdkPayload`, no pdb | 30 | 48.9 MB | 10.2 MB | 17.0 MB |
| Absolute minimum found by bisection | 28 | 47.4 MB | | |

Cold start to first window: 0.7 s cold, 0.3 s warm. Defender custom scan: clean.

**Two hooks used** (proven in a throwaway spike kept out of the repo; both go into the real
app csproj, and the whole target is reproduced at the bottom of this note):

1. Reference component packages instead of the meta-package:
   `Microsoft.WindowsAppSDK.Runtime` 2.4.0 + `Microsoft.WindowsAppSDK.WinUI` 2.3.6
   (pulls Base, Foundation, InteractiveExperiences, WebView2 transitively). This drops
   AI/ML (`onnxruntime.dll` 21 MB, `DirectML.dll` 18 MB, `Microsoft.Windows.AI.*`),
   Search, Widgets, DWrite from the payload entirely. Versions must match the ones the
   meta-package of the same release pins (see its nuspec), or
   `Microsoft.WindowsAppSDK.ComponentReference.targets` complains.
2. `TrimWindowsAppSdkPayload` target `AfterTargets="Publish"`: deny-list delete of
   files proven unnecessary. Deny-list on purpose: anything new stays in by default.
   (`MicrosoftWindowsAppSDKFilesExcluded` exists in `Microsoft.WindowsAppSDK.SelfContained.targets`
   too, but needs full-path identities; the post-publish delete is simpler.)

**Safe to delete (proven by running without them):** all `*.winmd` (reg-free WinRT
activation does NOT need them despite what the web says), all `*.json` (workloads),
all language folders except `en-us` (and probably that too), `Microsoft.WindowsAppRuntime.Bootstrap.dll`
(framework-dependent only), `RestartAgent.exe`, `WebView2Loader.dll`, `Microsoft.Web.WebView2.Core.dll`,
`WinUIEdit.dll` (RichEditBox only), `DWriteCore.dll`, `DwmSceneI.dll`, `*.ProxyStub.dll`,
`SessionHandleIPCProxyStub.dll`, `Microsoft.UI.Designer.dll`, `Microsoft.Windows.Workloads*`,
`Microsoft.UI.Xaml/Assets/*` (map.html, noise png).

**NOT safe (each one alone kills startup):**

- `Microsoft.ui.xaml.resources.common.dll` — **the hello-world bisection got this one wrong.**
  It ran fine without it; the real app (ListView, ToggleSwitch, InfoBar, FontIcon) dies at
  startup with `0xC000027B`, because this DLL carries the resource dictionaries for the standard
  controls. Removed from the deny-list on 2026-09-07. This is precisely the "re-run the
  bisection against the real app" warning below coming true, and it cost a wrong-diagnosis
  detour through [winui3-publish-does-not-copy-content-assets.md](winui3-publish-does-not-copy-content-assets.md).
  Payload cost: +1 file, +0.1 MB.

- `Microsoft.UI.Xaml.Internal.dll` — static import of `Microsoft.ui.xaml.dll` (`dumpbin /dependents`).
- `Microsoft.ui.xaml.resources.19h1.dll` — loaded by name at startup; without it
  `RoGetActivationFactory(Application)` fails with `0x8007007E`, which looks like a
  missing DLL elsewhere. This one cost the bisection a detour.
- `<App>.pri`, `Microsoft.UI.Xaml.Controls.pri`, `Microsoft.UI.pri`, `Microsoft.WindowsAppRuntime.pri`.

**Kept although not loaded by hello-world (small, likely needed by real controls):**
`Microsoft.UI.Xaml.Phone.dll` (1 MB), `Microsoft.UI.dll`, `Microsoft.Graphics.Display.dll`.
Re-run the bisection against the real app before shipping.

**Why "one exe" is not reachable here:** `PublishAot` and `PublishSingleFile` are
mutually exclusive; the WinUI/WinAppSDK natives ship no static libs; MRM does not read a
PRI embedded in the exe. The official `PublishSingleFile` mode of the SDK also just
self-extracts to a temp dir and redirects DLLs through `loadFrom='%MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY%...'`
in the SxS manifest (`Microsoft.WindowsAppSDK.SelfContained.targets`, `WindowsAppSDKRedirectDlls`).
The same mechanism could be reused by a hand-rolled self-extracting AOT exe; parked as an
optional later experiment.

**The target, verbatim,** so this note stands on its own:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="10.0.28000.2705" />
  <PackageReference Include="Microsoft.WindowsAppSDK.Runtime" Version="2.4.0" />
  <PackageReference Include="Microsoft.WindowsAppSDK.WinUI" Version="2.3.6" />
</ItemGroup>

<Target Name="TrimWindowsAppSdkPayload" AfterTargets="Publish">
  <ItemGroup>
    <_TrimFiles Include="$(PublishDir)*.winmd;$(PublishDir)*.json;$(PublishDir)*.pdb" />
    <_TrimFiles Include="$(PublishDir)onnxruntime.dll;$(PublishDir)DirectML.dll;$(PublishDir)NPUDetect.dll;$(PublishDir)PerceptiveStreaming.dll" />
    <_TrimFiles Include="$(PublishDir)Microsoft.Windows.AI.*.dll;$(PublishDir)Microsoft.Windows.Search.dll;$(PublishDir)Microsoft.Windows.Widgets.dll;$(PublishDir)Microsoft.Windows.Workloads*.dll;$(PublishDir)Microsoft.Windows.Workloads.pri" />
    <_TrimFiles Include="$(PublishDir)Microsoft.Web.WebView2.Core.dll;$(PublishDir)WebView2Loader.dll;$(PublishDir)WinUIEdit.dll;$(PublishDir)DWriteCore.dll;$(PublishDir)DwmSceneI.dll" />
    <_TrimFiles Include="$(PublishDir)Microsoft.WindowsAppRuntime.Bootstrap.dll;$(PublishDir)RestartAgent.exe;$(PublishDir)*.ProxyStub.dll;$(PublishDir)SessionHandleIPCProxyStub.dll;$(PublishDir)Microsoft.Windows.ApplicationModel.Background.UniversalBGTask.dll" />
    <_TrimFiles Include="$(PublishDir)Microsoft.UI.Designer.dll" />
    <_TrimDirs Include="$([System.IO.Directory]::GetDirectories('$(PublishDir)'))" Exclude="$(PublishDir)en-us;$(PublishDir)Assets" />
  </ItemGroup>
  <Delete Files="@(_TrimFiles)" />
  <RemoveDir Directories="@(_TrimDirs)" />
  <Message Importance="high" Text="TrimWindowsAppSdkPayload: removed @(_TrimFiles-&gt;Count()) files, @(_TrimDirs-&gt;Count()) dirs" />
</Target>
```
