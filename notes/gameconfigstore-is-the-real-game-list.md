# Game Bar's GameConfigStore is the only reliable list of what actually renders

Discovered 2026-09-08 while deciding how to find games. Confirmed against the live HKCU of a
machine with 108 children / 44 usable entries.

## Why not Steam/EGS

The obvious plan -- enumerate the Steam library and read `appmanifest_*.acf` -- fails on the
games that matter. Many launchers register one executable and render from another:

| Store says | Actually renders |
| --- | --- |
| `Wuthering Waves` app | `...\Client\Binaries\Win64\Client-Win64-Shipping.exe` |
| `Riftbreaker` | `bin\riftbreaker_win_release.exe`, not `bin\Launcher.exe` |
| `Control` | *two* exes, `Control_DX11.exe` and `Control_DX12.exe`, chosen at launch |

Auto HDR is keyed by the executable that owns the swapchain, so a store-derived path is
frequently the wrong one -- and silently so: the toggle is written, and nothing happens.
The test machine had exactly that, `Launcher.exe` configured for Riftbreaker.

## What Windows already records

Game Bar writes one subkey per detected game under `HKCU\System\GameConfigStore\Children\{guid}`,
naming the executable that actually renders rather than the one a store launches.

Detection is *not* by observation, as this note originally assumed. Game Bar matches running
processes against a list Microsoft ships, and a game whose install layout falls outside its entry
in that list is never recorded at all. The mechanism and its failure mode are in
[gamebar-matches-games-against-a-microsoft-list.md](gamebar-matches-games-against-a-microsoft-list.md).

| Value | Type | Notes |
| --- | --- | --- |
| `MatchedExeFullPath` | REG_SZ | full path; **absent on 64 of 108 children** |
| `LastAccessed` | REG_QWORD | FILETIME, UTC -- `DateTime.FromFileTimeUtc` |
| `TitleId` | REG_SZ | Microsoft's per-title id, stable across installs |
| `Flags`, `Type`, `Revision` | REG_DWORD | see below |
| `ExeParentDirectory`, `WorkingDirectory`, `Arguments` | REG_SZ | unreliable, see below |

`GameConfigStoreSource` reads exactly `MatchedExeFullPath`, `LastAccessed` and `TitleId`.

## The traps

**Most children are not games you have.** 64 of 108 carry no `MatchedExeFullPath` at all -- only
a `TitleId` and a `GameDVR_GameGUID`, for titles never installed on this machine (Football
Manager 2018 on a machine that has never seen it). They appear to arrive with the Microsoft
account. An entry without `MatchedExeFullPath` is not a game, it is noise.

**`Flags` is not a filter.** The tempting reading is that `17` means "real" and `51` means
"synced": 43 of the 44 real entries have `17`. But Warframe has `51` *with* a valid
`MatchedExeFullPath`, and so do the pathless ones. Filtering on it would drop a real game.
The only sound test is the presence of `MatchedExeFullPath`.

**Entries are never pruned, and never corrected.** 20 of 44 paths pointed at uninstalled games.
`File.Exists` is mandatory, which is why `GameConfigStoreSource` takes an injectable existence
predicate rather than calling `File.Exists` inline -- tests need to control it. A game that *moves*
is worse than one that is gone: the entry keeps the dead path forever, so the filter hides a game
that is installed and playable.

**Detection is not "uses D3D", so non-games get in.** VS Code (Electron) sits in the list. Only
one false positive out of 44, but there is no field that separates it: no flag, no type, nothing.
It has to be a checkbox in the UI, not a filter in the library.

**The naming fields are useless.** They look like a free display name and are not:

```
Client-Win64-Shipping.exe   ExeParentDirectory=common     WorkingDirectory=Wuthering Waves   <- good
VOIN-Win64-Shipping.exe     ExeParentDirectory=Binaries   WorkingDirectory=Win64             <- garbage
Subnautica.exe              (both empty)
```

They are not path components at all. On a list-matched child they are the *match constraints*
Microsoft authored for that title, which is why they look arbitrary -- see
[gamebar-matches-games-against-a-microsoft-list.md](gamebar-matches-games-against-a-microsoft-list.md).
`GameCandidate.DisplayName` derives the name from the path instead (`GameName.FromPath`), which at
least fails the same way every time.

**It depends on Game Bar.** `HKCU\System\GameConfigStore\GameDVR_Enabled` was `1` on the test
machine. Whether the list stops growing when Game Bar is disabled was not verified -- treat an
empty result as "no data", never as "no games".

## What does *not* work

**The D3D ETW channels are dead ends.** `Microsoft-Windows-DXGI/Logging`,
`Microsoft-Windows-Direct3D11/Logging` and `.../Direct3D12/Logging` all exist and are all
`enabled: false`, type `Analytic` -- they record nothing until explicitly enabled, and then into a
1 MB ring buffer. There is no retrospective D3D log to mine.
`Microsoft-Windows-DxgKrnl-Operational` is enabled but needs elevation to read.

**"Has d3d11.dll loaded" is not a game signal.** Measured across 416 live processes (module
enumeration itself needs no elevation, 0 access denials): `d3d11.dll` + `dxgi.dll` are loaded by
explorer, Telegram, Discord, WindowsTerminal, msedge, SearchHost, RuntimeBroker and every WinUI
app -- including this one. `d3d12.dll` narrows it to four processes, none of them a game.
Module scanning cannot classify. Use `RunningGameProbe` (GPU engine counters) instead.
