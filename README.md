# ForceAutoHDR.Net

[![CI](https://github.com/Phoenix-/ForceAutoHDR.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/Phoenix-/ForceAutoHDR.Net/actions/workflows/ci.yml)

Windows Auto HDR, per game, without the Settings app — a C# rewrite of
[7gxycn08/ForceAutoHDR](https://github.com/7gxycn08/ForceAutoHDR) on .NET 10 and Native AOT.

> **Status: 0.1.0, and there is something to download.** The core library is done and covered by
> tests, and the WinUI 3 app on top of it lists every configured game, toggles both mechanisms, and
> finds the games you actually play instead of asking you where they live. The
> [latest release](https://github.com/Phoenix-/ForceAutoHDR.Net/releases/latest) is a zip: unpack
> it anywhere and run `ForceAutoHDR.exe`. Native AOT and self-contained, so there is no installer
> and nothing to install alongside it, and no elevation prompt either — everything it touches lives
> under `HKCU`. Windows 11 22H2 or newer, x64.

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

## The app

One window, one row per configured game, two switches:

- **Auto HDR** — the per-game switch from Settings. Off writes an explicit "disabled"; the remove
  button leaves the game unconfigured instead, which is a different thing.
- **Force** — the `D3DBehaviors` override. Because it matches on the file name, flipping it on one
  row updates every other row with the same executable, right there in the list.

Games known only through an override — no path anywhere in the preferences — still get a row, with
the Auto HDR switch disabled and a label saying the entry is matched by file name alone.

Writes happen immediately; there is no Apply button, because neither mechanism is transactional and
both take effect the next time the game starts.

**Add game** does not ask you to go hunting for an executable. It lists what Windows has already
seen rendering, newest first, with the date it last ran. There is also a **Detect running game**
button that samples the GPU counters for whatever is drawing right now — the answer for a game
Windows never recorded — and a **Browse…** escape hatch.

When configured games are no longer installed, the main window offers to drop their entries. Only
the per-app preference goes; any `Force` override stays, because it matches on file name and a
second install of the same game may still be relying on it.

## Finding the games

Enumerating a Steam or Epic library is the obvious approach and it quietly gets the wrong answer.
Launcher-fronted games render from a different executable than the one the store starts, and Auto
HDR is keyed to the process that owns the swapchain — so the toggle gets written, and nothing
happens. This repo's own registry had `Launcher.exe` configured for Riftbreaker, doing nothing.

Games are discovered from three places instead, merged by path:

| Source | What it is |
|---|---|
| `GameConfigStore` | Game Bar's record of what Windows *observed* rendering, so it names the real executable — `Client-Win64-Shipping.exe` for Wuthering Waves, both `Control_DX11.exe` and `Control_DX12.exe` for Control |
| `UserGpuPreferences` | what is already configured, which also surfaces entries left behind by uninstalled games |
| GPU engine counters | what is rendering this second, for anything Game Bar missed |

Detection is by rendering, not by any manifest, so it is permissive on purpose: the occasional
Electron app turns up in the list. That is a checkbox to leave unticked, not something the library
should silently filter — and store metadata, when it lands, will sit on top of a discovered path
rather than replace it. The traps are in
[notes/gameconfigstore-is-the-real-game-list.md](notes/gameconfigstore-is-the-real-game-list.md).

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
dotnet publish src/ForceAutoHDR.App -c Release
```

.NET 10 SDK on Windows — the band is pinned in [global.json](global.json) — plus the MSVC toolchain
for the Native AOT link step. The link step also needs `vswhere.exe` on PATH -- publish from a
Developer PowerShell, or see
[notes/aot-link-step-needs-vswhere-on-path.md](notes/aot-link-step-needs-vswhere-on-path.md), which
also explains why the error blames the linker instead. The core library targets `net10.0-windows`,
is AOT-compatible, and pulls no packages at all; the app is unpackaged, self-contained WinUI 3 on
Windows App SDK 2.4.

The published payload is 30 files / 51 MB, trimmed from the stock 289 files / 131 MB by the
`TrimWindowsAppSdkPayload` target — pass `-p:SkipPayloadTrim=true` to publish it untouched, which
is the first thing to try if a published build misbehaves. What that target may and may not delete
is written up in [notes/winui3-aot-payload-trim.md](notes/winui3-aot-payload-trim.md), together
with the rest of what this stack cost to learn.

The app icon is not built either. [art/AppIcon.png](art/AppIcon.png) is the artwork and
[art/make-appicon.py](art/make-appicon.py) turns it into the ten-size
`src/ForceAutoHDR.App/Assets/AppIcon.ico` that `<ApplicationIcon>` embeds in the exe -- which is
also the only place the app has to read its own window icon back from, since publish does not copy
loose assets. Run the script by hand when the art changes (it wants Pillow and numpy, neither of
them part of the build); `--check` answers whether the committed `.ico` is still what the artwork
produces.

## Versions and releases

The git tag is the version of record, and pushing one is the entire release procedure. `v1.2.3`
makes the [release workflow](.github/workflows/release.yml) build with `-p:Version=1.2.3`, package
the published payload as `ForceAutoHDR-1.2.3-win-x64.zip` with a SHA256 beside it, and leave a
**draft** GitHub release for the notes to be written by hand before anything goes out. A tag with a
prerelease suffix — `v1.3.0-rc.1` — is marked as a prerelease; anything that is not
`vMAJOR.MINOR.PATCH` fails the workflow rather than shipping under a name nobody can parse.

Nothing in the tree gets bumped for a release. `VersionPrefix` in
[Directory.Build.props](Directory.Build.props) says which line is being worked on, and every build
that is not a release carries it with a `-dev` suffix plus the commit, so an exe you were handed
says `0.1.0-dev+9b1c3f2` in its own title bar and cannot pass for a release. Move
`VersionPrefix` on once a line has shipped.

[CI](.github/workflows/ci.yml) runs on every push and pull request: build, tests, and a full Native
AOT publish. The publish is there because the link step and the payload trim are the parts of this
stack that break, and neither runs during a plain build — the job also fails if the payload creeps
back toward the untrimmed 131 MB, which is what a Windows App SDK update renaming files out from
under a deny-list would look like. The published build is kept as a run artifact for 14 days.

## Credits

The registry mechanisms were established by [ledoge/autohdr_force](https://github.com/ledoge/autohdr_force)
and documented by Rafael Rivera; [7gxycn08/ForceAutoHDR](https://github.com/7gxycn08/ForceAutoHDR)
(Apache-2.0) is the tool this one is modelled on. No code was taken from either — this is an
independent implementation, and the debt is to the findings.

## License

MIT — see [LICENSE](LICENSE).
