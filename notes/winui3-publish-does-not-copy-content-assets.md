# `dotnet publish` does not copy Content assets next to an unpackaged WinUI 3 exe

**Trap (2026-09-07, Windows App SDK 2.4.0 / .NET 10 / NativeAOT, unpackaged):**

```xml
<Content Include="Assets\AppIcon.ico" />
```

lands `Assets\AppIcon.ico` in `bin\...\win-x64\` but **not** in `...\win-x64\publish\`. The
build output has the folder, the publish output does not. Verified with the payload-trim target
disabled (`-p:SkipPayloadTrim=true`) and with `-p:Platform=x64`, so neither the trim nor the
platform is responsible — publish simply never copies it. The asset is compiled into the app
PRI instead, reachable as `ms-appx:///Assets/...`, not as a file on disk.

**Symptom:** anything taking a *file system path* fails only in the published build.
`AppWindow.SetIcon("Assets/AppIcon.ico")` throws; because it is called from the `MainWindow`
constructor across the WinRT boundary, the process dies as a stowed exception —
`0xC000027B` in `Microsoft.UI.Xaml.dll`, no managed stack, empty stderr. Identical signature to
the missing-PRI trap, and easy to misdiagnose as that one.

**Also note:** `<ImageIconSource ImageSource="Assets/AppIcon.ico" />` on a `TitleBar` renders as
a broken-image placeholder even in the *build* output, where the file does exist.

**Fix used here:** stop depending on a loose file at all. `<ApplicationIcon>` already embeds the
icon into the exe, so the window icon is read back out of the executable itself:

```csharp
var icon = ExtractIcon(IntPtr.Zero, Environment.ProcessPath!, 0);
AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(icon));

[LibraryImport("shell32.dll", EntryPoint = "ExtractIconW", StringMarshalling = StringMarshalling.Utf16)]
private static partial IntPtr ExtractIcon(IntPtr hInst, string executablePath, int iconIndex);
```

`src/ForceAutoHDR.App/MainWindow.xaml.cs`, `SetIconFromExecutable`. The `Content` item was
dropped along with it — a 370 KB `.ico` in the PRI buys nothing once nothing references it.

**If a loose asset is genuinely needed**, publish it explicitly rather than relying on `Content`:

```xml
<None Include="Assets\Whatever.png" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
```

**Do not be fooled by a spike that "worked":** `spike/HelloAot`'s publish folder *did* contain
`Assets\`, which is what sent this diagnosis down the wrong path for a while. Publish folders are
not cleaned between runs, so it was a leftover from an earlier build. When a published app
behaves differently from the build output, compare the two file lists before theorising.

**Related:** `[LibraryImport]` needs `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (SYSLIB1062)
even when no code of yours is unsafe.
