# UserGpuPreferences is not only apps, and D3DBehaviors is not keyed by path

Two shape surprises found while writing `ForceAutoHDR.Core` (2026-09-07), both confirmed
against the live HKCU of a machine with ~30 configured games.

## `UserGpuPreferences` mixes applications and housekeeping values

`HKCU\Software\Microsoft\DirectX\UserGpuPreferences` holds one REG_SZ per configured
application, named after the executable's **full path**. But Windows parks its own values in
the same key:

```
DirectXUserGlobalSettings          = AutoHDREnable=1;VRROptimizeEnable=1;SwapEffectUpgradeEnable=1;
GraphicsFeaturesNotificationConfig = 1
```

Note `AutoHDREnable=1` in the global entry versus `2097`/`2096` per app — the same flag name
means different things depending on which value it lives in, so do not reuse the per-app
decoder for the global one.

A deny-list of known names goes stale the moment Windows adds a third. The guard is instead
`Path.IsPathFullyQualified(valueName)` in `GpuPreferencesStore.IsApplicationValue`, which also
rejects a caller trying to write a bare `game.exe`, an entry Windows would silently never match.

## The two mechanisms are keyed differently, and it shows in the UI

- `UserGpuPreferences` → keyed by full path. Two installs of the same game are two entries.
- `Direct3D\<subkey>\Name` → keyed by the **bare file name**. One override hits every process
  with that name, wherever it lives.

This is not theoretical: the test machine has Wuthering Waves installed twice
(`...\Steam\steamapps\common\...` and `...\Wuthering Waves\...`), both matched by the single
`Client-Win64-Shipping` subkey. `AutoHdrService.GetProfiles` therefore reports `IsForced` on
*every* path whose file name matches, and an override with no matching path becomes its own row
with `ExecutablePath == null`. Do not "fix" that into a one-to-one merge — it would lie about
what the registry does.

## Flag strings must round-trip

Both mechanisms store `Key=Value;` soup, and Windows writes flags this app knows nothing about
(`SwapEffectUpgradeEnable`, `DXGIEffects`, `AppStatus`, `GpuPreference`) into the very same
value. `FlagString` preserves order, key casing and unknown flags, including whether the source
ended with a `;` — Windows uses a trailing separator under `UserGpuPreferences` and none for
`D3DBehaviors`, and a round trip should not rewrite what it did not mean to change.
