# ForceAutoHDR.Net

Windows Auto HDR, per game, without the Settings app — a C# rewrite of
[7gxycn08/ForceAutoHDR](https://github.com/7gxycn08/ForceAutoHDR) on .NET 10 and Native AOT.

> **Status: early.** The core library is done and covered by tests. The WinUI 3 app that will
> sit on top of it does not exist yet. Nothing is released, and there is nothing to download.

## Why

Windows decides on its own which games get Auto HDR, and the per-game switch is buried three
clicks deep in Settings — one game at a time, with no list of what you have already configured.
Two registry mechanisms drive the whole thing, and both are just strings under `HKCU`.

The rewrite is also a deliberate probe of the WinUI 3 + Native AOT + .NET 10 stack: how far it
has actually come, and what it costs to ship. Whatever hurts along the way is written down in
[`notes/`](notes/) rather than forgotten.

## The two mechanisms

They are independent, and a tool that pretends otherwise will confuse you sooner or later.

| | Per-app graphics preference | `D3DBehaviors` override |
|---|---|---|
| Key | `HKCU\Software\Microsoft\DirectX\UserGpuPreferences` | `HKCU\Software\Microsoft\Direct3D\<subkey>` |
| Identified by | the executable's **full path** (the value name) | the executable's **bare file name** (the `Name` value) |
| Payload | `AutoHDREnable=2097;` on, `2096;` off | `BufferUpgradeOverride=1;BufferUpgradeEnable10Bit=1` |
| What it is | the switch the Settings page writes | forces the D3D swapchain to 10-bit so Auto HDR engages for titles Windows will not offer it for |
| Documented | yes | no — via [ledoge/autohdr_force](https://github.com/ledoge/autohdr_force) and Rafael Rivera's 2020 write-up |

Consequences the library takes seriously:

- **One override can force several games.** It matches on the file name, so two installs of the
  same title — or two unrelated games that both ship `Launcher.exe` — are hit by one subkey.
  `AutoHdrService.GetProfiles()` reports that honestly instead of merging it away.
- **Windows writes flags you did not.** `SwapEffectUpgradeEnable`, `DXGIEffects`, `AppStatus`,
  `GpuPreference` live in the very same value. `FlagString` round-trips order, key casing and
  unknown flags, down to whether the string ended with a `;`.
- **Not everything under the key is an app.** `DirectXUserGlobalSettings` and
  `GraphicsFeaturesNotificationConfig` share it, so entries are recognised by being a fully
  qualified path rather than by a deny-list that goes stale.
- **No elevation, ever.** Everything is `HKCU`. The original tool's admin check was unnecessary.

## Using the library

```csharp
using ForceAutoHDR.Core;

var service = AutoHdrService.ForCurrentUser();

foreach (var profile in service.GetProfiles())
{
    Console.WriteLine($"{profile.DisplayName}: {profile.AutoHdr}, forced={profile.IsForced}");
}

service.SetAutoHdr(@"C:\Games\Steam\steamapps\common\X4 Foundations\X4.exe", AutoHdrState.Enabled);
service.SetForced(@"D:\Games\Arknights Endfield\Endfield.exe", forced: true);
```

Registry access sits behind `IRegistryStore`, so the whole library runs against
`InMemoryRegistryStore` — that is how the tests work, and how the UI will get design-time data
and a dry run.

## Building

```
dotnet test
```

.NET 10 SDK, Windows. The library targets `net10.0-windows` and is AOT-compatible; it pulls no
packages at all.

## Credits

The registry mechanisms were established by [ledoge/autohdr_force](https://github.com/ledoge/autohdr_force)
and documented by Rafael Rivera; [7gxycn08/ForceAutoHDR](https://github.com/7gxycn08/ForceAutoHDR)
(Apache-2.0) is the tool this one is modelled on. No code was taken from either — this is an
independent implementation, and the debt is to the findings.

## License

MIT — see [LICENSE](LICENSE).
