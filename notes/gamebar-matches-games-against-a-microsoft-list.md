# Game Bar does not observe games, it matches them against a Microsoft list

Discovered 2026-09-08, chasing a game that had vanished from the add dialog. Corrects the
central claim of [gameconfigstore-is-the-real-game-list.md](gameconfigstore-is-the-real-game-list.md),
which assumed detection was by observed rendering. It is not.

## The mechanism

`GameBar.exe` (from the `Microsoft.XboxGamingOverlay` package) downloads a **Known Game List**
and matches running processes against it. Strings straight out of the binary:

```
GameBar::KnownGameListController::UpdateKGLIfNecessary
C:\__w\1\s\Source\GameBar\KnownGameList\KnownGameListController.cpp
https://dlassets-ssl.xboxlive.com/public/content/kgl/
kgl.bin / KnownGameList.bin / kglDL.bin
IsNewKGLVersionAvailable: OneSettingsVersion: %u, BCastDVRVersion: %u
KGL Data did not match expected hash
InitializeFtObjects: Initializing m_gameConfigStoreFT
```

| Where | What |
| --- | --- |
| `%LOCALAPPDATA%\Microsoft\GameDVR\KnownGameList.bin` | the downloaded list (1.85 MB on the test machine) |
| `C:\Windows\bcastdvr\KnownGameList.bin` | OS baseline copy, much smaller (483 KB) and **not** a superset |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR\KGLRevision` | version of the downloaded list |
| `...\GameDVR\KGLToGCSUpdatedRevision` | revision already projected into the **G**ame**C**onfig**S**tore |

The revision is a little-endian `uint32` at byte offset 8 of the `.bin`: `83 0a 00 00` = 2691,
matching `KGLRevision` exactly. Writes into `GameConfigStore` go through a full-trust helper
(`m_gameConfigStoreFT` → `GameBarFTServer.exe`), not from `GameBar.exe` directly.

Records are UTF-16 inside the `.bin`, shaped `<exeName> <0..2 directory names> <GUID> <TitleId>`:

```
client-win64-shipping.exe | common | Wuthering Waves | 7d4e6639-... | 1980190385
wwm.exe                   | common | Where Winds Meet | 88a733be-... | 1693534474
genshinimpact.exe         | Genshin Impact Game       | a45347a2-... | 1962957406
zenlesszonezero.exe       | (no constraints)          | 63005c76-... | 1713923056
```

The directory names are **match constraints**, not observed path components. A generic
executable name gets qualified by the folders Microsoft expects around it.

## Two kinds of children, and how to tell them apart

`Revision` is the discriminator:

| | KGL match | observed / "Remember this is a game" |
| --- | --- | --- |
| `Revision` | the KGL revision (`2691`) | `1` |
| `TitleId`, `GameDVR_GameGUID` | present | **absent** |
| `ExeParentDirectory` | a bare constraint name (`common`) | the **full** directory path |
| `WorkingDirectory` | a bare constraint name | absent |
| `Arguments` | absent | the real command line |

This is why the naming fields looked "populated inconsistently" in the older note: on a KGL
child they were never path components in the first place. Verified field-for-field against six
titles on the live hive.

## The trap: a moved game is invisible forever

Wuthering Waves' KGL record requires a directory named `common` -- i.e. Steam's
`steamapps\common\...`. Reinstalled under its own Kuro launcher the path became
`C:\Games\Wuthering Waves\Wuthering Waves Game\Client\Binaries\Win64\Client-Win64-Shipping.exe`,
which has no `common` component, so **no match, no entry, ever**. The game ran for minutes with
the overlay opened over it and Game Bar wrote nothing; `LastGameActivity` did not move.

The stale entry naming the deleted Steam path stayed exactly as it was. `MatchedExeFullPath` is
never updated when a game moves, so `GameConfigStoreSource` produced a candidate with
`ExecutableExists = false`, which `GameDiscoveryService.GetCandidates` then dropped -- and the
game disappeared from the add dialog entirely.

Positive control: Genshin Impact lives at a non-Steam `D:\Games\HoYoPlay\...` and *is* detected,
because its KGL record carries no `common` constraint. So the failure is the constraint, not the
drive or the launcher.

This is a whole class, not one game: any title moved between a store install and its own
launcher can fall out of its KGL constraints.

Deleting the stale child does not help. Game Bar keys children by executable path, not by title
(Control has two children sharing one `TitleId`), so the old entry was never blocking a new one.

## What actually works

**`RunningGameProbe`.** The GPU engine counters saw the game at 44 % on `engtype_3d`, and
`ProcessImage.GetPath` resolved the correct current path. This is the only mechanism that needs
nothing from Microsoft.

**Win+G → Settings → General → "Remember this is a game".** Writes a `Revision = 1` child with
the correct current path, bypassing the KGL. Verified: children went 108 → 109, and the new entry
named the real executable. Worth surfacing in the UI as advice, since it also fixes Game Bar
itself rather than only our list.

There is no documented API for a third party to register a game with Game Bar.

## Bonus: the anti-cheat right that matters

Wuthering Waves runs under ACE (Anti-Cheat Expert), which strips `PROCESS_VM_READ`, so
`Process.MainModule` (and `Get-Process | .Path`) fails on it. `OpenProcess` with
`PROCESS_QUERY_LIMITED_INFORMATION` + `QueryFullProcessImageName` still returns the path -- which
is exactly why `Interop/ProcessImage` is written the way it is. Confirmed live against the
protected process. Note this was *not* the cause of the detection failure; it only looked like a
suspect.
